import { Camera, X, CheckCircle2, PencilLine, AlertTriangle, ChevronDown, ShieldCheck, Bell, Upload, ArrowLeft } from "lucide-react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { api, OcrImagePreviewResponse, FieldConfidence } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type ScanMode = "choose" | "camera" | "manual" | "review";

type DetectedItem = {
  name: string;
  categoryName: string;
  expiryDate: string;
  daysLeftToExpire: number;
  confidenceScore: number;
};

type ReviewForm = {
  name: string;
  categoryName: string;
  manufacturingDate: string;
  expiryDate: string;
  quantity: number;
  rawText: string;
};

const ALERT_STORAGE_KEY = "foodtrack_product_alerts";
const PRODUCT_IMAGES_KEY = "foodtrack_product_images";
const PREF_KEY = "foodtrack_preferences";

function isInSilentHours(): boolean {
  try {
    const prefs = JSON.parse(localStorage.getItem(PREF_KEY) || "{}");
    if (!prefs.silentHours) return false;
    const now = new Date();
    const currentMinutes = now.getHours() * 60 + now.getMinutes();
    const [sh, sm] = (prefs.silentStart || "22:00").split(":").map(Number);
    const [eh, em] = (prefs.silentEnd || "07:00").split(":").map(Number);
    const start = sh * 60 + sm;
    const end = eh * 60 + em;
    if (start <= end) return currentMinutes >= start && currentMinutes < end;
    return currentMinutes >= start || currentMinutes < end;
  } catch { return false; }
}

function saveProductAlert(productName: string, expiryDate: string, timing: string) {
  if (timing === "none") return;
  const silent = isInSilentHours();
  const alerts = JSON.parse(localStorage.getItem(ALERT_STORAGE_KEY) || "[]");
  alerts.push({ productName, expiryDate, alertType: timing, silent, createdAt: new Date().toISOString() });
  localStorage.setItem(ALERT_STORAGE_KEY, JSON.stringify(alerts));
}

function saveProductImage(productName: string, dataUrl: string) {
  const images: Record<string, string> = JSON.parse(localStorage.getItem(PRODUCT_IMAGES_KEY) || "{}");
  images[productName.toLowerCase().trim()] = dataUrl;
  localStorage.setItem(PRODUCT_IMAGES_KEY, JSON.stringify(images));
}

function fileToDataUrl(file: Blob | File): Promise<string> {
  return new Promise((resolve) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.readAsDataURL(file);
  });
}

const CATEGORIES = [
  "General", "Dairy", "Fruits", "Vegetables", "Meat", "Bakery Item",
  "Snacks", "Grains", "Beverages", "Condiments", "Frozen",
];

const CATEGORY_FALLBACK_DAYS: Record<string, number> = {
  Dairy: 14, Meat: 3, Fruits: 5, Vegetables: 7, "Bakery Item": 5,
  Snacks: 90, Grains: 180, Beverages: 90, Condiments: 180, Frozen: 90, General: 30,
};

function getCategoryFallbackDate(category: string): string {
  const days = CATEGORY_FALLBACK_DAYS[category] ?? 30;
  return new Date(Date.now() + days * 86400000).toISOString().slice(0, 10);
}

function normalizeApiDate(input?: string | null): string {
  if (!input) return "";
  const datePart = input.slice(0, 10);
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(datePart);
  if (!m) return "";
  const year = Number(m[1]);
  const month = Number(m[2]);
  const day = Number(m[3]);
  if (year < 2000 || year > 2100) return "";
  if (month < 1 || month > 12) return "";
  if (day < 1 || day > 31) return "";
  return datePart;
}

function clampDate(y: number, m: number, d: number): string {
  const max = new Date(y, m, 0).getDate();
  const day = Math.min(Math.max(1, d), max);
  return `${String(y).padStart(4, "0")}-${String(m).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

function disambiguateDayMonth(a: number, b: number, y: number): string | null {
  if (y < 2000 || y > 2100) return null;
  if (a > 12 && b >= 1 && b <= 12) return clampDate(y, b, a);
  if (b > 12 && a >= 1 && a <= 12) return clampDate(y, a, b);
  if (a >= 1 && a <= 12 && b >= 1 && b <= 12) return clampDate(y, b, a);
  if (b >= 1 && b <= 12) return clampDate(y, b, a);
  return null;
}

function extractDateFromRaw(raw: string, labelRegex: RegExp): string | null {
  const text = raw.replace(/\|/g, "/");
  const lm = labelRegex.exec(text);
  const source = lm?.[1] ?? text;
  const dm = /(\d{1,2})[\/.-](\d{1,2})[\/.-](\d{2,4})/.exec(source);
  if (!dm) return null;
  let a = Number(dm[1]);
  let b = Number(dm[2]);
  let y = Number(dm[3]);
  if (y < 100) y += 2000;
  return disambiguateDayMonth(a, b, y);
}

function extractAllDatesFromRaw(raw: string): string[] {
  const text = raw.replace(/\|/g, "/");
  const regex = /(\d{1,2})[\/.-](\d{1,2})[\/.-](\d{2,4})/g;
  const found: string[] = [];
  let match: RegExpExecArray | null;
  while ((match = regex.exec(text)) !== null) {
    let a = Number(match[1]);
    let b = Number(match[2]);
    let y = Number(match[3]);
    if (y < 100) y += 2000;
    const result = disambiguateDayMonth(a, b, y);
    if (result) found.push(result);
  }
  return [...new Set(found)];
}

function chooseBestFutureDate(dates: string[]): string | null {
  if (dates.length === 0) return null;
  const today = new Date().toISOString().slice(0, 10);
  const futures = dates.filter((d) => d >= today).sort();
  if (futures.length > 0) return futures[futures.length - 1];
  return dates.sort()[dates.length - 1];
}

function extractBestBeforeExpiry(raw: string, manufacturingDate: string): string | null {
  if (!manufacturingDate) return null;
  const m = /(?:best\s*before|use\s*(?:with)?in|consume\s*(?:with)?in|shelf\s*life|valid\s*for|good\s*for)\s*(?:within\s*)?(\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|fifteen|eighteen|twenty\s*four|thirty)\s*(day|days|week|weeks|month|months|year|years|hr|hrs|hours)/i.exec(raw);
  if (!m) return null;
  const words: Record<string, number> = {
    one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, eight: 8,
    nine: 9, ten: 10, eleven: 11, twelve: 12, fifteen: 15, eighteen: 18,
    "twenty four": 24, twentyfour: 24, thirty: 30,
  };
  const v = Number(m[1]) || words[m[1].toLowerCase().replace(/\s+/g, " ")] || 0;
  if (v <= 0) return null;
  const base = new Date(`${manufacturingDate}T00:00:00`);
  const unit = m[2].toLowerCase();
  if (unit.startsWith("day")) base.setDate(base.getDate() + v);
  else if (unit.startsWith("week")) base.setDate(base.getDate() + v * 7);
  else if (unit.startsWith("month")) base.setMonth(base.getMonth() + v);
  else if (unit.startsWith("year")) base.setFullYear(base.getFullYear() + v);
  else if (unit.startsWith("hr") || unit.startsWith("hour")) base.setDate(base.getDate() + Math.max(1, Math.ceil(v / 24)));
  return base.toISOString().slice(0, 10);
}

function extractProductCandidates(raw: string, extractedName: string): string[] {
  const set = new Set<string>();
  const blocked = /(exp|expiry|best before|mfg|mfd|manufactur|date|batch|lot|upc|ean|vpn|\d{1,2}[\/.\-|]\d{1,2}[\/.\-|]\d{2,4})/i;
  const splitBy = /,|\/|&|\+|\band\b/gi;
  const push = (value: string) => {
    const cleaned = value.trim().replace(/\s+/g, " ");
    if (!cleaned || cleaned.length < 2 || cleaned.length > 60) return;
    if (blocked.test(cleaned)) return;
    set.add(cleaned);
  };
  if (extractedName && extractedName !== "Unknown Product") {
    extractedName.split(splitBy).forEach(push);
  }
  raw.replace(/\r/g, "\n").split("\n").map((l) => l.trim()).filter(Boolean).forEach((line) => line.split(splitBy).forEach(push));
  return Array.from(set).slice(0, 8);
}

function mergeCandidates(...lists: Array<string[] | null | undefined>): string[] {
  const set = new Set<string>();
  for (const list of lists) {
    if (!list) continue;
    for (const value of list) {
      const cleaned = value.trim().replace(/\s+/g, " ");
      if (!cleaned || cleaned.length < 2 || cleaned.length > 60) continue;
      set.add(cleaned);
    }
  }
  return Array.from(set).slice(0, 12);
}

function inferCategory(name: string): string {
  const l = name.toLowerCase();
  if (/milk|cheese|butter|yogurt|cream|paneer|curd/.test(l)) return "Dairy";
  if (/bread|bun|cake|pastry|croissant/.test(l)) return "Bakery Item";
  if (/biscuit|cookie|chocolate|chips|wafer|snack/.test(l)) return "Snacks";
  if (/banana|apple|orange|mango|grape|papaya/.test(l)) return "Fruits";
  if (/chicken|beef|fish|mutton|pork|meat|prawn/.test(l)) return "Meat";
  if (/tomato|onion|potato|carrot|spinach|broccoli/.test(l)) return "Vegetables";
  if (/rice|pasta|noodle|oats|cereal|wheat|flour/.test(l)) return "Grains";
  if (/juice|soda|water|tea|coffee|drink/.test(l)) return "Beverages";
  return "General";
}

function ConfidenceBadge({ score }: { score: number }) {
  if (score >= 70) return <span className="text-[10px] font-bold text-green-600 dark:text-green-400 bg-green-50 dark:bg-green-500/10 px-2 py-0.5 rounded-full">High ({score}%)</span>;
  if (score >= 40) return <span className="text-[10px] font-bold text-orange-600 dark:text-orange-400 bg-orange-50 dark:bg-orange-500/10 px-2 py-0.5 rounded-full">Medium ({score}%)</span>;
  return <span className="text-[10px] font-bold text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-500/10 px-2 py-0.5 rounded-full">Low ({score}%) - Verify</span>;
}

export default function Scan() {
  const [searchParams] = useSearchParams();
  const sourceParam = searchParams.get("source") || "home";
  const modeParam = searchParams.get("mode");
  const closePath = sourceParam === "pantry" ? "/pantry" : "/";

  const uploadInputRef = useRef<HTMLInputElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const autoStartedRef = useRef(false);

  const [mode, setMode] = useState<ScanMode>("camera");
  const [streaming, setStreaming] = useState(false);
  const [error, setError] = useState("");
  const [preview, setPreview] = useState<OcrImagePreviewResponse | null>(null);
  const [images, setImages] = useState<Array<{ file: Blob | File; name: string }>>([]);
  const [capturedPreviewUrl, setCapturedPreviewUrl] = useState<string | null>(null);
  const [productCandidates, setProductCandidates] = useState<string[]>([]);
  const [detectedItems, setDetectedItems] = useState<DetectedItem[]>([]);
  const [showSplitEdit, setShowSplitEdit] = useState(false);
  const [fieldConfidence, setFieldConfidence] = useState<FieldConfidence | null>(null);
  const [needsHumanReview, setNeedsHumanReview] = useState(false);
  const [humanVerified, setHumanVerified] = useState(false);

  const [alertTiming, setAlertTiming] = useState<string>("1d");
  const [productImageUrl, setProductImageUrl] = useState<string | null>(null);
  const [productImageFile, setProductImageFile] = useState<File | null>(null);
  const [manualScanning, setManualScanning] = useState(false);
  const productImageInputRef = useRef<HTMLInputElement>(null);

  const [reviewForm, setReviewForm] = useState<ReviewForm>({
    name: "",
    categoryName: "General",
    manufacturingDate: "",
    expiryDate: new Date().toISOString().slice(0, 10),
    quantity: 1,
    rawText: "",
  });

  const { token } = useAuth();
  const qc = useQueryClient();
  const navigate = useNavigate();

  const invalidateAll = useCallback(() => {
    qc.invalidateQueries({ queryKey: ["pantry"] });
    qc.invalidateQueries({ queryKey: ["products-home"] });
    qc.invalidateQueries({ queryKey: ["dashboard"] });
    qc.invalidateQueries({ queryKey: ["recipes"] });
  }, [qc]);

  const stopCamera = useCallback(() => {
    const stream = videoRef.current?.srcObject as MediaStream | null;
    if (stream) stream.getTracks().forEach((t) => t.stop());
    if (videoRef.current) videoRef.current.srcObject = null;
    setStreaming(false);
  }, []);

  const startCamera = useCallback(async () => {
    setError("");
    const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
    const constraints: MediaStreamConstraints[] = isIOS
      ? [
          // iOS Safari needs exact facingMode first
          { video: { facingMode: { exact: "environment" } }, audio: false },
          { video: { facingMode: "environment" }, audio: false },
          { video: true, audio: false },
        ]
      : [
          { video: { facingMode: "environment", width: { ideal: 1920 }, height: { ideal: 1080 } }, audio: false },
          { video: { facingMode: "environment", width: { ideal: 1280 }, height: { ideal: 720 } }, audio: false },
          { video: { facingMode: "environment" }, audio: false },
          { video: true, audio: false },
        ];
    for (const constraint of constraints) {
      try {
        const stream = await navigator.mediaDevices.getUserMedia(constraint);
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          // Wait for metadata before playing to improve cross-browser reliability
          await new Promise<void>((resolve) => {
            const video = videoRef.current!;
            const onLoaded = () => { video.removeEventListener("loadedmetadata", onLoaded); resolve(); };
            video.addEventListener("loadedmetadata", onLoaded);
            setTimeout(() => { video.removeEventListener("loadedmetadata", onLoaded); resolve(); }, 3000);
          });
          await videoRef.current.play();
          // Watch for stream interruption (e.g., iOS background/lock)
          stream.getTracks().forEach((track) => {
            track.onended = () => {
              setStreaming(false);
              // Attempt auto-recovery if page is visible
              setTimeout(() => {
                if (document.visibilityState === "visible") {
                  startCamera();
                } else {
                  setError("Camera stream ended. Tap to restart.");
                }
              }, 1000);
            };
          });
        }
        setStreaming(true);
        setMode("camera");
        return;
      } catch (err) {
        if (err instanceof DOMException) {
          if (err.name === "NotAllowedError") {
            setError("Camera permission denied. Please allow camera access in app settings.");
            return;
          }
          if (err.name === "NotFoundError") {
            setError("No camera found on this device. Use file upload instead.");
            return;
          }
        }
        // Try next constraint set
      }
    }
    setError("Unable to access camera. Check app permissions or use file upload.");
  }, []);

  const cropCenterForOcr = useCallback(async (source: CanvasImageSource, width: number, height: number) => {
    const out = document.createElement("canvas");
    const ctx = out.getContext("2d");
    if (!ctx) return null;
    const cropW = Math.floor(width * 0.82);
    const cropH = Math.floor(height * 0.58);
    const sx = Math.floor((width - cropW) / 2);
    const sy = Math.floor((height - cropH) / 2);
    out.width = cropW;
    out.height = cropH;
    ctx.drawImage(source, sx, sy, cropW, cropH, 0, 0, cropW, cropH);
    return await new Promise<Blob | null>((resolve) => out.toBlob(resolve, "image/jpeg", 0.98));
  }, []);

  const previewMutation = useMutation({
    mutationFn: async (scanImages: Array<{ file: Blob | File; name: string }>) => {
      if (scanImages.length === 0) throw new Error("Please add at least one image.");
      return api.scanMultiPreview(token!, scanImages.map((i) => ({ file: i.file, fileName: i.name })), reviewForm.quantity);
    },
    onSuccess: (data) => {
      setPreview(data);
      setMode("review");
      setError("");
      const raw = data.rawText ?? "";
      const mfgFromRaw = extractDateFromRaw(raw, /(?:mfg|mfd|manufactur(?:ed|ing)|packed(?:\s*on)?|prod(?:uction)?\s*date)\s*[:\-]?\s*([^\n]+)/i);
      const expFromRaw = extractDateFromRaw(raw, /(?:exp|expiry|expires|use\s*by|best\s*before|use\s*before|consume\s*before)\s*[:\-]?\s*([^\n]+)/i);
      const allRawDates = extractAllDatesFromRaw(raw);
      const normalizedMfg = mfgFromRaw || normalizeApiDate(data.extracted.manufacturingDate);
      let normalizedExp = expFromRaw || chooseBestFutureDate(allRawDates) || normalizeApiDate(data.extracted.expiryDate);
      if (!normalizedExp) normalizedExp = extractBestBeforeExpiry(raw, normalizedMfg);
      const detectedCategory = data.extracted.categoryName || "General";
      const fallbackExp = getCategoryFallbackDate(detectedCategory);

      setReviewForm((prev) => ({
        ...prev,
        name: data.extracted.productName || "Unknown Product",
        categoryName: detectedCategory,
        manufacturingDate: normalizedMfg,
        expiryDate: normalizedExp || fallbackExp,
        rawText: data.rawText,
      }));

      const extractedCandidates = data.extracted.productCandidates ?? [];
      const textCandidates = extractProductCandidates(raw, data.extracted.productName || "Unknown Product");
      const candidates = mergeCandidates(extractedCandidates, textCandidates);
      setProductCandidates(candidates);

      // Parse field confidence and human review flags
      const fc = data.extracted.fieldConfidence ?? null;
      setFieldConfidence(fc);
      const reviewNeeded = data.extracted.needsHumanReview ?? false;
      setNeedsHumanReview(reviewNeeded);
      setHumanVerified(false);

      // Per-item detected items from server or built from candidates
      const serverItems = data.detectedItems ?? [];
      if (serverItems.length > 0) {
        setDetectedItems(serverItems.map((item) => ({
          name: item.productName,
          categoryName: item.categoryName,
          expiryDate: normalizeApiDate(item.expiryDate) || getCategoryFallbackDate(item.categoryName),
          daysLeftToExpire: item.daysLeftToExpire,
          confidenceScore: item.confidenceScore,
        })));
      } else if (candidates.length > 1) {
        setDetectedItems(candidates.map((name) => {
          const cat = inferCategory(name);
          return {
            name, categoryName: cat,
            expiryDate: name === data.extracted.productName ? (normalizedExp || fallbackExp) : getCategoryFallbackDate(cat),
            daysLeftToExpire: 0, confidenceScore: (data.extracted as any).confidenceScore ?? 0,
          };
        }));
      } else {
        setDetectedItems([]);
      }

      if (candidates.length > 0 && (!data.extracted.productName || data.extracted.productName === "Unknown Product")) {
        setReviewForm((prev) => ({ ...prev, name: candidates[0] }));
      } else if (candidates.length > 0 && data.extracted.productName?.includes(",")) {
        setReviewForm((prev) => ({ ...prev, name: candidates[0] }));
      }
      stopCamera();
    },
    onError: (e) => setError(e instanceof Error ? e.message : "OCR failed"),
  });

  const addMutation = useMutation({
    mutationFn: () => api.addProduct(token!, {
      name: reviewForm.name, categoryName: reviewForm.categoryName,
      expiryDate: reviewForm.expiryDate, quantity: reviewForm.quantity,
    }),
    onSuccess: async () => {
      saveProductAlert(reviewForm.name, reviewForm.expiryDate, alertTiming);
      // Save captured image for pantry display
      if (images.length > 0) {
        try {
          const dataUrl = await fileToDataUrl(images[0].file);
          saveProductImage(reviewForm.name, dataUrl);
        } catch { /* ignore */ }
      }
      invalidateAll();
      navigate("/pantry");
    },
    onError: (e) => setError(e instanceof Error ? e.message : "Add failed"),
  });

  const splitAddMutation = useMutation({
    mutationFn: async () => {
      const items = detectedItems.length > 0 ? detectedItems : productCandidates.map((name) => ({
        name, categoryName: inferCategory(name),
        expiryDate: getCategoryFallbackDate(inferCategory(name)),
        daysLeftToExpire: 0, confidenceScore: 0,
      }));
      await Promise.all(items.map((item) => api.addProduct(token!, {
        name: item.name, categoryName: item.categoryName,
        expiryDate: item.expiryDate, quantity: Math.max(1, reviewForm.quantity),
      })));
    },
    onSuccess: () => { invalidateAll(); navigate("/pantry"); },
    onError: (e) => setError(e instanceof Error ? e.message : "Add failed"),
  });

  const daysLeft = useMemo(() => {
    const expiry = new Date(reviewForm.expiryDate);
    return Math.ceil((expiry.getTime() - Date.now()) / 86400000);
  }, [reviewForm.expiryDate]);

  const confidenceScore = (preview?.extracted as any)?.confidenceScore ?? (preview?.extracted?.isConfidenceLow ? 25 : 70);

  useEffect(() => {
    return () => {
      stopCamera();
      if (capturedPreviewUrl) URL.revokeObjectURL(capturedPreviewUrl);
    };
  }, [capturedPreviewUrl, stopCamera]);

  useEffect(() => {
    if (!autoStartedRef.current) {
      autoStartedRef.current = true;
      if (modeParam === "manual") {
        setMode("manual");
      } else if (modeParam === "upload") {
        // Delay slightly so the ref is mounted
        setTimeout(() => uploadInputRef.current?.click(), 300);
      } else {
        if (navigator.mediaDevices?.getUserMedia) {
          startCamera();
        } else {
          setError("Camera not available. Use file upload instead.");
        }
      }
    }
  }, [startCamera, modeParam]);

  // Handle orientation changes to restart camera with correct resolution
  useEffect(() => {
    const handleOrientationChange = () => {
      if (streaming && videoRef.current?.srcObject) {
        stopCamera();
        setTimeout(() => startCamera(), 300);
      }
    };
    window.addEventListener("orientationchange", handleOrientationChange);
    screen.orientation?.addEventListener("change", handleOrientationChange);
    return () => {
      window.removeEventListener("orientationchange", handleOrientationChange);
      screen.orientation?.removeEventListener("change", handleOrientationChange);
    };
  }, [streaming, stopCamera, startCamera]);

  // Auto-recover camera when returning from background/lock screen
  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.visibilityState === "visible" && mode === "camera" && !streaming) {
        startCamera();
      }
    };
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () => document.removeEventListener("visibilitychange", handleVisibilityChange);
  }, [mode, streaming, startCamera]);

  const processCaptured = useCallback((captured: Array<{ file: Blob | File; name: string }>) => {
    setImages(captured);
    previewMutation.mutate(captured);
  }, [previewMutation]);

  const captureSnapshot = useCallback(async () => {
    if (!videoRef.current || !canvasRef.current) return;
    const video = videoRef.current;
    const canvas = canvasRef.current;
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    const blob = await cropCenterForOcr(canvas, canvas.width, canvas.height);
    if (!blob) return;
    const captured = { file: blob, name: `capture-${images.length + 1}.jpg` };
    const previewUrl = URL.createObjectURL(blob);
    if (capturedPreviewUrl) URL.revokeObjectURL(capturedPreviewUrl);
    setCapturedPreviewUrl(previewUrl);
    stopCamera();
    processCaptured([captured]);
  }, [images.length, capturedPreviewUrl, stopCamera, cropCenterForOcr, processCaptured]);

  const onFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const picked = Array.from(e.target.files ?? []);
    if (picked.length === 0) return;
    Promise.all(
      picked.map(async (file) => {
        const bitmap = await createImageBitmap(file);
        const cropped = await cropCenterForOcr(bitmap, bitmap.width, bitmap.height);
        bitmap.close();
        return { file: cropped ?? file, name: file.name };
      }),
    ).then((processed) => {
      if (processed.length > 0) {
        const previewUrl = URL.createObjectURL(processed[0].file);
        if (capturedPreviewUrl) URL.revokeObjectURL(capturedPreviewUrl);
        setCapturedPreviewUrl(previewUrl);
        stopCamera();
        processCaptured(processed.slice(0, 4));
      }
    }).catch(() => {
      for (const file of picked) {
        setImages((prev) => prev.length >= 4 ? prev : [...prev, { file, name: file.name }]);
      }
    });
    e.target.value = "";
  }, [capturedPreviewUrl, stopCamera, cropCenterForOcr, processCaptured]);

  const updateDetectedItem = useCallback((index: number, field: keyof DetectedItem, value: string) => {
    setDetectedItems((prev) => prev.map((item, i) => i === index ? { ...item, [field]: value } : item));
  }, []);

  const removeDetectedItem = useCallback((index: number) => {
    setDetectedItems((prev) => prev.filter((_, i) => i !== index));
  }, []);

  const onProductImageChange = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (productImageUrl) URL.revokeObjectURL(productImageUrl);
    setProductImageUrl(URL.createObjectURL(file));
    setProductImageFile(file);
    e.target.value = "";

    // Run OCR on the uploaded image to auto-fill fields
    setManualScanning(true);
    setError("");
    try {
      const data = await api.scanMultiPreview(token!, [{ file, fileName: file.name }], 1);
      const raw = data.rawText ?? "";
      const mfgFromRaw = extractDateFromRaw(raw, /(?:mfg|mfd|manufactur(?:ed|ing)|packed(?:\s*on)?|prod(?:uction)?\s*date)\s*[:\-]?\s*([^\n]+)/i);
      const expFromRaw = extractDateFromRaw(raw, /(?:exp|expiry|expires|use\s*by|best\s*before|use\s*before|consume\s*before)\s*[:\-]?\s*([^\n]+)/i);
      const allRawDates = extractAllDatesFromRaw(raw);
      const normalizedMfg = mfgFromRaw || normalizeApiDate(data.extracted.manufacturingDate);
      let normalizedExp = expFromRaw || chooseBestFutureDate(allRawDates) || normalizeApiDate(data.extracted.expiryDate);
      if (!normalizedExp) normalizedExp = extractBestBeforeExpiry(raw, normalizedMfg);

      const detectedName = data.extracted.productName || "";
      const detectedCategory = data.extracted.categoryName || (detectedName ? inferCategory(detectedName) : "General");
      const fallbackExp = getCategoryFallbackDate(detectedCategory);

      setReviewForm((prev) => ({
        ...prev,
        name: detectedName || prev.name,
        categoryName: detectedCategory,
        manufacturingDate: normalizedMfg || prev.manufacturingDate,
        expiryDate: normalizedExp || fallbackExp,
      }));
    } catch {
      // OCR failed silently — user can still fill manually
    } finally {
      setManualScanning(false);
    }
  }, [productImageUrl, token]);

  // Auto-set category when product name changes
  const handleNameChange = useCallback((value: string) => {
    setReviewForm((s) => {
      const cat = inferCategory(value);
      return { ...s, name: value, categoryName: cat !== "General" ? cat : s.categoryName };
    });
  }, []);

  // Save product image to localStorage on add
  const handleManualAdd = useCallback(async () => {
    if (productImageFile) {
      const dataUrl = await fileToDataUrl(productImageFile);
      saveProductImage(reviewForm.name, dataUrl);
    }
    addMutation.mutate();
  }, [productImageFile, reviewForm.name, addMutation]);

  // ── Manual entry mode: clean standalone form ──
  if (mode === "manual") {
    return (
      <div className="flex flex-col h-full animate-in fade-in duration-300 pb-28">
        <div className="flex items-center gap-3 px-6 pt-6 pb-4">
          <button onClick={() => navigate(closePath)} className="w-10 h-10 rounded-full bg-muted flex items-center justify-center">
            <ArrowLeft className="h-5 w-5 text-foreground" />
          </button>
          <h1 className="text-xl font-bold text-foreground">Add Item</h1>
        </div>

        <div className="flex-1 overflow-y-auto no-scrollbar px-6 pb-6">
          <div className="bg-card rounded-2xl border border-border p-5 grid gap-4 shadow-sm">
            {/* Product image upload */}
            <div className="flex flex-col items-center gap-2">
              <button
                onClick={() => productImageInputRef.current?.click()}
                className="w-24 h-24 rounded-2xl border-2 border-dashed border-border bg-muted/30 flex flex-col items-center justify-center gap-1 hover:bg-muted/50 transition-colors overflow-hidden"
              >
                {productImageUrl ? (
                  <img src={productImageUrl} alt="product" className="w-full h-full object-cover" />
                ) : (
                  <>
                    <Upload className="h-6 w-6 text-muted-foreground" />
                    <span className="text-[10px] text-muted-foreground font-medium">Add Photo</span>
                  </>
                )}
              </button>
              {manualScanning && (
                <p className="text-[10px] text-primary font-medium animate-pulse">Scanning image...</p>
              )}
              {productImageUrl && !manualScanning && (
                <button onClick={() => { if (productImageUrl) URL.revokeObjectURL(productImageUrl); setProductImageUrl(null); setProductImageFile(null); }} className="text-[10px] text-red-500 font-medium">
                  Remove Photo
                </button>
              )}
              <input ref={productImageInputRef} type="file" accept="image/*" className="hidden" onChange={onProductImageChange} />
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium mb-1 block">Product Name</label>
              <Input placeholder="e.g. Milk, Bread, Eggs" value={reviewForm.name} onChange={(e) => handleNameChange(e.target.value)} className="rounded-xl" />
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium mb-1 block">Category</label>
              <select className="h-10 rounded-xl border border-input px-3 text-sm bg-background text-foreground w-full" value={reviewForm.categoryName} onChange={(e) => setReviewForm((s) => ({ ...s, categoryName: e.target.value }))}>
                {CATEGORIES.map((cat) => <option key={cat} value={cat}>{cat}</option>)}
              </select>
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium mb-1 block">Expiry Date</label>
              <Input type="date" value={reviewForm.expiryDate} onChange={(e) => setReviewForm((s) => ({ ...s, expiryDate: e.target.value }))} className="rounded-xl" />
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium mb-1 block">Quantity</label>
              <Input type="number" min={1} value={reviewForm.quantity} onChange={(e) => setReviewForm((s) => ({ ...s, quantity: Number(e.target.value) || 1 }))} className="rounded-xl" />
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium flex items-center gap-1 mb-1.5">
                <Bell className="h-3 w-3" /> Expiry Alert
              </label>
              <div className="flex flex-wrap gap-1.5">
                {[
                  { value: "7d", label: "7 Days Before" },
                  { value: "3d", label: "3 Days Before" },
                  { value: "1d", label: "1 Day Before" },
                  { value: "on_expiry", label: "On Expiry Day" },
                  { value: "none", label: "No Alert" },
                ].map((opt) => (
                  <button
                    key={opt.value}
                    onClick={() => setAlertTiming(opt.value)}
                    className={`px-3 py-1.5 rounded-full text-[11px] font-semibold border transition-colors ${
                      alertTiming === opt.value
                        ? "bg-primary/10 border-primary text-primary"
                        : "bg-card border-border text-muted-foreground hover:bg-muted/50"
                    }`}
                  >
                    {opt.label}
                  </button>
                ))}
              </div>
            </div>

            <Button
              onClick={handleManualAdd}
              disabled={addMutation.isPending || !reviewForm.name.trim() || manualScanning}
              className="rounded-2xl h-12 font-bold mt-1"
            >
              <CheckCircle2 className="h-4 w-4 mr-2" />
              {addMutation.isPending ? "Saving..." : "Add to Pantry"}
            </Button>

            {error && <p className="text-xs text-red-600 text-center">{error}</p>}
          </div>
        </div>
      </div>
    );
  }

  // ── Camera / Review mode ──
  return (
    <div className="flex-1 flex flex-col p-6 gap-6 h-full animate-in zoom-in duration-300 pb-28">
      {mode !== "review" && (
        <div className="flex-1 bg-black rounded-[3rem] flex flex-col items-center justify-center text-white relative overflow-hidden shadow-2xl">
          <div className="absolute inset-0 border-[2px] border-primary/40 m-12 rounded-[2rem]">
            <div className="absolute top-0 left-0 w-8 h-8 border-t-4 border-l-4 border-primary rounded-tl-xl" />
            <div className="absolute top-0 right-0 w-8 h-8 border-t-4 border-r-4 border-primary rounded-tr-xl" />
            <div className="absolute bottom-0 left-0 w-8 h-8 border-b-4 border-l-4 border-primary rounded-bl-xl" />
            <div className="absolute bottom-0 right-0 w-8 h-8 border-b-4 border-r-4 border-primary rounded-br-xl" />
            <div className="absolute left-4 right-4 h-0.5 bg-primary/80 shadow-[0_0_15px_rgba(46,204,113,0.8)] animate-scan" />
          </div>

          {streaming ? (
            <video ref={videoRef} className="absolute inset-0 w-full h-full object-cover" muted playsInline />
          ) : capturedPreviewUrl ? (
            <img src={capturedPreviewUrl} alt="captured" className="absolute inset-0 w-full h-full object-cover" />
          ) : (
            <>
              <Camera className="h-16 w-16 opacity-30 mb-4" />
              <div className="text-center px-8 z-10 flex flex-col gap-2">
                <p className="text-sm font-bold tracking-wide">Point camera at the product</p>
                <p className="text-[10px] opacity-60">Capture the expiry date label clearly</p>
              </div>
            </>
          )}

          <div className="absolute bottom-10 flex gap-8 items-center px-8 w-full justify-center z-20">
            <button onClick={streaming ? captureSnapshot : startCamera} className="w-20 h-20 rounded-full border-4 border-white flex items-center justify-center p-1 active:scale-95 transition-transform">
              <div className="w-full h-full rounded-full bg-card shadow-lg" />
            </button>
          </div>

          <button onClick={() => navigate(closePath)} className="absolute top-6 right-6 w-10 h-10 rounded-full bg-card/10 backdrop-blur-md flex items-center justify-center z-20">
            <X className="h-5 w-5 text-white" />
          </button>

          <input ref={uploadInputRef} type="file" accept="image/*" multiple capture="environment" className="hidden" onChange={onFileChange} />
          <canvas ref={canvasRef} className="hidden" />
        </div>
      )}

      {mode === "camera" && previewMutation.isPending && (
        <div className="text-center text-sm text-muted-foreground font-medium animate-pulse">Processing image...</div>
      )}

      {mode === "review" && (
        <div className="bg-card rounded-2xl border p-4 grid gap-3">
          <div className="flex items-center justify-between">
            <p className="text-sm font-semibold flex items-center gap-2"><PencilLine className="h-4 w-4" />Review and Edit</p>
            {preview && <ConfidenceBadge score={confidenceScore} />}
          </div>

          {(needsHumanReview || confidenceScore < 40) && (
            <div className="bg-amber-50 dark:bg-amber-500/10 border border-amber-200 dark:border-amber-500/20 rounded-xl p-3 flex items-start gap-2">
              <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400 mt-0.5 flex-shrink-0" />
              <p className="text-[11px] text-amber-800 dark:text-amber-200">
                {needsHumanReview ? "Some fields have low confidence. Please verify" : "Low confidence extraction. Please verify"}
                {fieldConfidence && (
                  <span>
                    {fieldConfidence.nameConfidence === "low" && " the product name,"}
                    {fieldConfidence.expiryConfidence === "low" && " the expiry date,"}
                    {fieldConfidence.categoryConfidence === "low" && " the category,"}
                  </span>
                )} before saving.
              </p>
            </div>
          )}

          <div>
            <div className="flex items-center justify-between mb-0.5">
              <label className="text-[10px] text-muted-foreground font-medium">Product Name</label>
              {fieldConfidence?.nameConfidence && (
                <span className={`text-[9px] font-semibold ${fieldConfidence.nameConfidence === "high" ? "text-green-600" : fieldConfidence.nameConfidence === "medium" ? "text-amber-600" : "text-red-600"}`}>
                  {fieldConfidence.nameConfidence} confidence
                </span>
              )}
            </div>
            <Input
              placeholder="Product name" value={reviewForm.name}
              onChange={(e) => setReviewForm((s) => ({ ...s, name: e.target.value }))}
              className={fieldConfidence?.nameConfidence === "low" ? "border-red-400 ring-1 ring-red-200" : fieldConfidence?.nameConfidence === "medium" ? "border-amber-400 ring-1 ring-amber-200" : ""}
            />
          </div>
          {productCandidates.length > 1 && (
            <select className="h-10 rounded-xl border border-input px-3 text-sm bg-background text-foreground" value={reviewForm.name} onChange={(e) => setReviewForm((s) => ({ ...s, name: e.target.value }))}>
              {productCandidates.map((name) => <option key={name} value={name}>{name}</option>)}
            </select>
          )}

          <div>
            <div className="flex items-center justify-between mb-0.5">
              <label className="text-[10px] text-muted-foreground font-medium">Category</label>
              {fieldConfidence?.categoryConfidence && (
                <span className={`text-[9px] font-semibold ${fieldConfidence.categoryConfidence === "high" ? "text-green-600" : fieldConfidence.categoryConfidence === "medium" ? "text-amber-600" : "text-red-600"}`}>
                  {fieldConfidence.categoryConfidence} confidence
                </span>
              )}
            </div>
            <select
              className={`h-10 rounded-xl border border-input px-3 text-sm bg-background text-foreground w-full ${fieldConfidence?.categoryConfidence === "low" ? "border-red-400 ring-1 ring-red-200" : fieldConfidence?.categoryConfidence === "medium" ? "border-amber-400 ring-1 ring-amber-200" : ""}`}
              value={reviewForm.categoryName} onChange={(e) => setReviewForm((s) => ({ ...s, categoryName: e.target.value }))}
            >
              {CATEGORIES.map((cat) => <option key={cat} value={cat}>{cat}</option>)}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="text-[10px] text-muted-foreground font-medium">Mfg Date</label>
              <Input type="date" value={reviewForm.manufacturingDate} onChange={(e) => setReviewForm((s) => ({ ...s, manufacturingDate: e.target.value }))} />
            </div>
            <div>
              <div className="flex items-center justify-between mb-0.5">
                <label className="text-[10px] text-muted-foreground font-medium">Expiry Date</label>
                {fieldConfidence?.expiryConfidence && (
                  <span className={`text-[9px] font-semibold ${fieldConfidence.expiryConfidence === "high" ? "text-green-600" : fieldConfidence.expiryConfidence === "medium" ? "text-amber-600" : "text-red-600"}`}>
                    {fieldConfidence.expiryConfidence} confidence
                  </span>
                )}
              </div>
              <Input
                type="date" value={reviewForm.expiryDate}
                onChange={(e) => setReviewForm((s) => ({ ...s, expiryDate: e.target.value }))}
                className={fieldConfidence?.expiryConfidence === "low" ? "border-red-400 ring-1 ring-red-200" : fieldConfidence?.expiryConfidence === "medium" ? "border-amber-400 ring-1 ring-amber-200" : ""}
              />
            </div>
          </div>

          <Input type="number" min={1} value={reviewForm.quantity} onChange={(e) => setReviewForm((s) => ({ ...s, quantity: Number(e.target.value) || 1 }))} placeholder="Quantity" />

          <div>
            <label className="text-[10px] text-muted-foreground font-medium flex items-center gap-1 mb-1.5">
              <Bell className="h-3 w-3" /> Expiry Alert
            </label>
            <div className="flex flex-wrap gap-1.5">
              {[
                { value: "7d", label: "7 Days Before" },
                { value: "3d", label: "3 Days Before" },
                { value: "1d", label: "1 Day Before" },
                { value: "on_expiry", label: "On Expiry Day" },
                { value: "none", label: "No Alert" },
              ].map((opt) => (
                <button
                  key={opt.value}
                  onClick={() => setAlertTiming(opt.value)}
                  className={`px-3 py-1.5 rounded-full text-[11px] font-semibold border transition-colors ${
                    alertTiming === opt.value
                      ? "bg-primary/10 border-primary text-primary"
                      : "bg-card border-border text-muted-foreground hover:bg-muted/50"
                  }`}
                >
                  {opt.label}
                </button>
              ))}
            </div>
          </div>

          <div className="text-xs text-muted-foreground">
            <p>Days left: <strong className={daysLeft <= 3 ? "text-red-600" : daysLeft <= 7 ? "text-orange-600" : "text-green-600"}>{daysLeft}</strong></p>
          </div>

          {reviewForm.rawText && (
            <details className="text-xs text-muted-foreground">
              <summary className="cursor-pointer">View extracted raw text</summary>
              <pre className="whitespace-pre-wrap mt-2 bg-muted p-2 rounded-xl max-h-32 overflow-auto">{reviewForm.rawText}</pre>
            </details>
          )}

          {needsHumanReview && (
            <label className="flex items-center gap-2 text-xs cursor-pointer select-none">
              <input type="checkbox" checked={humanVerified} onChange={(e) => setHumanVerified(e.target.checked)} className="rounded border-gray-300 accent-primary" />
              <ShieldCheck className="h-3.5 w-3.5 text-primary" />
              I've verified the details above are correct
            </label>
          )}

          <Button onClick={() => addMutation.mutate()} disabled={addMutation.isPending || (needsHumanReview && !humanVerified)} className="rounded-2xl">
            <CheckCircle2 className="h-4 w-4 mr-2" />
            {addMutation.isPending ? "Saving..." : "Confirm and Add to Pantry"}
          </Button>

          <Button variant="outline" className="rounded-2xl" onClick={() => {
            setMode("camera");
            setPreview(null);
            setCapturedPreviewUrl(null);
            setDetectedItems([]);
            setImages([]);
            setHumanVerified(false);
            setFieldConfidence(null);
            setNeedsHumanReview(false);
            startCamera();
          }}>
            <Camera className="h-4 w-4 mr-2" />
            Retake Photo
          </Button>

          {(detectedItems.length > 1 || productCandidates.length > 1) && (
            <>
              <button onClick={() => setShowSplitEdit(!showSplitEdit)} className="flex items-center justify-between w-full text-xs font-medium text-primary px-1">
                <span>Split & Add All ({detectedItems.length || productCandidates.length} items)</span>
                <ChevronDown className={`h-3 w-3 transition-transform ${showSplitEdit ? "rotate-180" : ""}`} />
              </button>

              {showSplitEdit && detectedItems.length > 0 && (
                <div className="space-y-2 border rounded-xl p-3 bg-gray-50">
                  {detectedItems.map((item, idx) => (
                    <div key={idx} className="grid grid-cols-[1fr_auto_auto_auto] gap-1 items-center text-xs">
                      <Input value={item.name} onChange={(e) => updateDetectedItem(idx, "name", e.target.value)} className="h-8 text-xs" />
                      <select value={item.categoryName} onChange={(e) => updateDetectedItem(idx, "categoryName", e.target.value)} className="h-8 rounded border border-input px-1 text-[10px] bg-background text-foreground w-20">
                        {CATEGORIES.map((cat) => <option key={cat} value={cat}>{cat}</option>)}
                      </select>
                      <Input type="date" value={item.expiryDate} onChange={(e) => updateDetectedItem(idx, "expiryDate", e.target.value)} className="h-8 text-[10px] w-28" />
                      <button onClick={() => removeDetectedItem(idx)} className="text-red-400 hover:text-red-600 p-1"><X className="h-3 w-3" /></button>
                    </div>
                  ))}
                </div>
              )}

              <Button variant="outline" onClick={() => splitAddMutation.mutate()} disabled={splitAddMutation.isPending} className="rounded-2xl">
                {splitAddMutation.isPending ? "Adding all..." : `Split & Add All (${detectedItems.length || productCandidates.length})`}
              </Button>
            </>
          )}
        </div>
      )}

      {error && <p className="text-xs text-red-600 text-center">{error}</p>}
      <p className="text-xs text-muted-foreground text-center">Tip: add multiple clear images for better scan quality.</p>
    </div>
  );
}
