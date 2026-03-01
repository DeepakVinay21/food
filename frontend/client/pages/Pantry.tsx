import { Search, Filter, Trash2, Plus, Clock, ChevronDown, Check, Pencil, X, Bell, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { toast } from "sonner";
import { Accordion, AccordionItem, AccordionTrigger, AccordionContent } from "@/components/ui/accordion";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger, DropdownMenuLabel } from "@/components/ui/dropdown-menu";
import { cn } from "@/lib/utils";
import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, Product } from "@/lib/api";
import { useAuth } from "@/components/auth/AuthProvider";
import { useNavigate, useSearchParams } from "react-router-dom";
import { imageByName } from "@/lib/productImages";
import { useTranslation } from "@/lib/i18n/LanguageContext";
import type { TranslationKeys } from "@/lib/i18n/translations/en";

const ALERT_STORAGE_KEY = "foodtrack_product_alerts";

const ALL_CATEGORIES = "All Categories";

function getProductAlertTiming(productName: string): string {
  try {
    const alerts = JSON.parse(localStorage.getItem(ALERT_STORAGE_KEY) || "[]");
    const match = [...alerts].reverse().find((a: { productName: string }) =>
      a.productName.toLowerCase() === productName.toLowerCase()
    );
    return match?.alertType || "none";
  } catch { return "none"; }
}

function saveProductAlert(productName: string, expiryDate: string, timing: string) {
  if (timing === "none") {
    // Remove existing alerts for this product
    try {
      const alerts = JSON.parse(localStorage.getItem(ALERT_STORAGE_KEY) || "[]");
      const filtered = alerts.filter((a: { productName: string }) =>
        a.productName.toLowerCase() !== productName.toLowerCase()
      );
      localStorage.setItem(ALERT_STORAGE_KEY, JSON.stringify(filtered));
    } catch { /* ignore */ }
    return;
  }
  try {
    const alerts = JSON.parse(localStorage.getItem(ALERT_STORAGE_KEY) || "[]");
    // Remove old alerts for this product, then add updated one
    const filtered = alerts.filter((a: { productName: string }) =>
      a.productName.toLowerCase() !== productName.toLowerCase()
    );
    filtered.push({ productName, expiryDate, alertType: timing, silent: false, createdAt: new Date().toISOString() });
    localStorage.setItem(ALERT_STORAGE_KEY, JSON.stringify(filtered));
  } catch { /* ignore */ }
}

// ---------- Types ----------

type StatusType = "expired" | "expiring" | "safe";

type EnrichedBatch = {
  batchId: string;
  expiryDate: string;
  quantity: number;
  days: number;
  status: StatusType;
};

type ProductGroup = {
  productId: string;
  name: string;
  categoryName: string;
  image: string | null;
  enrichedBatches: EnrichedBatch[];
  earliestExpiry: string;
  worstStatus: StatusType;
  totalQuantity: number;
  earliestDays: number;
};

type TFunc = (key: TranslationKeys, vars?: Record<string, string | number>) => string;

const CATEGORIES = [
  "General", "Dairy", "Fruits", "Vegetables", "Meat", "Bakery Item",
  "Snacks", "Grains", "Beverages", "Condiments", "Frozen",
];

const CATEGORY_TRANSLATION_KEYS: Record<string, TranslationKeys> = {
  "General": "category.general",
  "Dairy": "category.dairy",
  "Fruits": "category.fruits",
  "Vegetables": "category.vegetables",
  "Meat": "category.meat",
  "Bakery Item": "category.bakeryItem",
  "Snacks": "category.snacks",
  "Grains": "category.grains",
  "Beverages": "category.beverages",
  "Condiments": "category.condiments",
  "Frozen": "category.frozen",
};

function categoryLabel(category: string, t: TFunc): string {
  const key = CATEGORY_TRANSLATION_KEYS[category];
  return key ? t(key) : category;
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

// ---------- Helpers ----------

const CATEGORY_COLORS: Record<string, string> = {
  Dairy: "bg-blue-500",
  Fruits: "bg-orange-500",
  Vegetables: "bg-green-500",
  Meat: "bg-red-500",
  "Bakery Item": "bg-amber-600",
  Snacks: "bg-purple-500",
  Grains: "bg-yellow-600",
  Beverages: "bg-cyan-500",
  Condiments: "bg-pink-500",
  Frozen: "bg-sky-400",
  General: "bg-primary",
};

const LetterAvatar = ({ name, categoryName }: { name: string; categoryName: string }) => {
  const letter = (name || "?")[0].toUpperCase();
  const bg = CATEGORY_COLORS[categoryName] || "bg-primary";
  return (
    <div className={cn("w-full h-full flex items-center justify-center", bg)}>
      <span className="text-white font-bold text-2xl select-none">{letter}</span>
    </div>
  );
};

const ProductAvatar = ({ name, image, categoryName }: { name: string; image: string | null; categoryName: string }) => {
  const [imgError, setImgError] = useState(false);
  if (image && !imgError) {
    return <img src={image} alt={name} className="w-full h-full object-cover hover:scale-110 transition-transform duration-500" onError={() => setImgError(true)} />;
  }
  return <LetterAvatar name={name} categoryName={categoryName} />;
};

const getStatusColor = (s: string) => {
  switch (s) {
    case "expired": return "bg-red-500";
    case "expiring": return "bg-orange-500";
    default: return "bg-primary";
  }
};

const statusLabel = (status: StatusType, days: number, t: TFunc) => {
  if (status === "expired") return t("pantry.statusExpired");
  if (status === "expiring") return days === 0 ? t("pantry.statusToday") : t("pantry.statusDaysLeft", { days });
  return t("pantry.statusSafe");
};

const statusTextColor = (status: StatusType) => {
  if (status === "expired") return "text-red-500";
  if (status === "expiring") return "text-orange-500";
  return "text-primary";
};

const progressWidth = (days: number) => `${Math.max(15, 100 - Math.max(days, 0) * 10)}%`;

function groupProducts(products: Product[]): ProductGroup[] {
  const today = new Date();
  return products
    .map((product) => {
      const enrichedBatches: EnrichedBatch[] = product.batches
        .map((b) => {
          const days = Math.ceil((new Date(b.expiryDate).getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
          const status: StatusType = days < 0 ? "expired" : days <= 2 ? "expiring" : "safe";
          return { batchId: b.batchId, expiryDate: b.expiryDate, quantity: b.quantity, days, status };
        })
        .sort((a, b) => new Date(a.expiryDate).getTime() - new Date(b.expiryDate).getTime());

      const earliest = enrichedBatches[0];

      const worstStatus: StatusType = enrichedBatches.some((b) => b.status === "expired")
        ? "expired"
        : enrichedBatches.some((b) => b.status === "expiring")
          ? "expiring"
          : "safe";

      return {
        productId: product.productId,
        name: product.name,
        categoryName: product.categoryName,
        image: imageByName(product.name, product.categoryName),
        enrichedBatches,
        earliestExpiry: earliest?.expiryDate ?? "",
        worstStatus,
        totalQuantity: enrichedBatches.reduce((sum, b) => sum + b.quantity, 0),
        earliestDays: earliest?.days ?? 0,
      };
    })
    .filter((g) => g.enrichedBatches.length > 0)
    .sort((a, b) => new Date(a.earliestExpiry).getTime() - new Date(b.earliestExpiry).getTime());
}

// ---------- Components ----------

const SingleBatchCard = ({ group, onDeleteProduct, onEdit, isDeletingProduct, t }: { group: ProductGroup; onDeleteProduct: (productId: string) => void; onEdit: (group: ProductGroup, batch: EnrichedBatch) => void; isDeletingProduct: boolean; t: TFunc }) => {
  const batch = group.enrichedBatches[0];
  const [confirmDelete, setConfirmDelete] = useState(false);
  return (
    <div className="bg-card rounded-[2rem] p-4 border border-border shadow-sm flex flex-col gap-3 animate-in slide-in-from-left duration-300">
      <div className="flex items-center gap-4">
        <div className="w-20 h-20 rounded-3xl bg-primary/10 overflow-hidden flex-shrink-0 shadow-inner flex items-center justify-center">
          <ProductAvatar name={group.name} image={group.image} categoryName={group.categoryName} />
        </div>
        <div className="flex-1 flex flex-col gap-1">
          <h3 className="font-bold text-foreground text-base tracking-tight">{group.name} (x{batch.quantity})</h3>
          <p className="text-[11px] text-muted-foreground flex items-center gap-1 font-medium">
            <Clock className="h-3 w-3" />
            {t("pantry.expires", { date: batch.expiryDate })} &bull; {categoryLabel(group.categoryName, t)}
          </p>
          <div className="mt-2 flex items-center gap-3">
            <div className="h-1.5 flex-1 rounded-full bg-muted overflow-hidden">
              <div className={cn("h-full rounded-full transition-all duration-1000", getStatusColor(batch.status))} style={{ width: progressWidth(batch.days) }} />
            </div>
            <span className={cn("text-[10px] font-bold uppercase tracking-wider", statusTextColor(batch.status))}>
              {statusLabel(batch.status, batch.days, t)}
            </span>
          </div>
        </div>
      </div>
      <div className="flex gap-2 border-t border-border pt-3">
        <button
          className="flex-1 h-9 rounded-xl bg-blue-500/10 text-blue-600 dark:text-blue-400 text-xs font-bold flex items-center justify-center gap-1.5 hover:bg-blue-500/20 active:scale-95 transition-all"
          onClick={() => onEdit(group, batch)}
        >
          <Pencil className="h-3.5 w-3.5" /> {t("pantry.edit")}
        </button>
        {confirmDelete ? (
          <div className="flex-1 flex gap-2">
            <button
              className="flex-1 h-9 rounded-xl bg-destructive text-destructive-foreground text-xs font-bold flex items-center justify-center gap-1.5 active:scale-95 disabled:opacity-50"
              disabled={isDeletingProduct}
              onClick={() => { onDeleteProduct(group.productId); setConfirmDelete(false); }}
            >
              {isDeletingProduct ? "..." : t("pantry.confirm")}
            </button>
            <button
              className="flex-1 h-9 rounded-xl bg-muted text-muted-foreground text-xs font-bold flex items-center justify-center active:scale-95"
              onClick={() => setConfirmDelete(false)}
            >
              {t("pantry.cancel")}
            </button>
          </div>
        ) : (
          <button
            className="flex-1 h-9 rounded-xl bg-red-500/10 text-red-600 dark:text-red-400 text-xs font-bold flex items-center justify-center gap-1.5 hover:bg-red-500/20 active:scale-95 transition-all"
            onClick={() => setConfirmDelete(true)}
          >
            <Trash2 className="h-3.5 w-3.5" /> {t("pantry.delete")}
          </button>
        )}
      </div>
    </div>
  );
};

const BatchSubCard = ({ batch, onDelete, onEdit, isDeleting, t }: { batch: EnrichedBatch; onDelete: (batchId: string) => void; onEdit: (batch: EnrichedBatch) => void; isDeleting: boolean; t: TFunc }) => {
  const [confirmDelete, setConfirmDelete] = useState(false);
  return (
    <div className="bg-muted/50 rounded-2xl p-3 border border-border/50 flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <p className="text-xs text-muted-foreground flex items-center gap-1 font-medium">
          <Clock className="h-3 w-3" />
          Exp: {batch.expiryDate} &nbsp;x{batch.quantity}
        </p>
      </div>
      <div className="flex items-center gap-3">
        <div className="h-1.5 flex-1 rounded-full bg-background overflow-hidden">
          <div className={cn("h-full rounded-full transition-all duration-1000", getStatusColor(batch.status))} style={{ width: progressWidth(batch.days) }} />
        </div>
        <span className={cn("text-[10px] font-bold uppercase tracking-wider", statusTextColor(batch.status))}>
          {statusLabel(batch.status, batch.days, t)}
        </span>
      </div>
      <div className="flex gap-2 pt-1">
        <button
          className="flex-1 h-7 rounded-lg bg-blue-500/10 text-blue-600 dark:text-blue-400 text-[10px] font-bold flex items-center justify-center gap-1 hover:bg-blue-500/20 active:scale-95 transition-all"
          onClick={() => onEdit(batch)}
        >
          <Pencil className="h-3 w-3" /> {t("pantry.edit")}
        </button>
        {confirmDelete ? (
          <div className="flex-1 flex gap-1">
            <button
              className="flex-1 h-7 rounded-lg bg-destructive text-destructive-foreground text-[10px] font-bold flex items-center justify-center active:scale-95 disabled:opacity-50"
              disabled={isDeleting}
              onClick={() => { onDelete(batch.batchId); setConfirmDelete(false); }}
            >
              {isDeleting ? "..." : t("pantry.confirm")}
            </button>
            <button
              className="flex-1 h-7 rounded-lg bg-muted text-muted-foreground text-[10px] font-bold flex items-center justify-center active:scale-95"
              onClick={() => setConfirmDelete(false)}
            >
              {t("pantry.cancel")}
            </button>
          </div>
        ) : (
          <button
            className="flex-1 h-7 rounded-lg bg-red-500/10 text-red-600 dark:text-red-400 text-[10px] font-bold flex items-center justify-center gap-1 hover:bg-red-500/20 active:scale-95 transition-all"
            onClick={() => setConfirmDelete(true)}
          >
            <Trash2 className="h-3 w-3" /> {t("pantry.delete")}
          </button>
        )}
      </div>
    </div>
  );
};

const MultiBatchCard = ({ group, onDelete, onDeleteProduct, onEdit, isDeleting, isDeletingProduct, t }: { group: ProductGroup; onDelete: (batchId: string) => void; onDeleteProduct: (productId: string) => void; onEdit: (group: ProductGroup, batch: EnrichedBatch) => void; isDeleting: boolean; isDeletingProduct: boolean; t: TFunc }) => {
  const [confirmDeleteAll, setConfirmDeleteAll] = useState(false);
  return (
    <Accordion type="single" collapsible className="animate-in slide-in-from-left duration-300">
      <AccordionItem value={group.productId} className="border-none">
        <div className="bg-card rounded-[2rem] border border-border shadow-sm overflow-hidden">
          <AccordionTrigger className="group hover:no-underline p-0 [&>svg]:hidden">
            <div className="p-4 flex items-center gap-4 w-full">
              <div className="w-20 h-20 rounded-3xl bg-primary/10 overflow-hidden flex-shrink-0 shadow-inner">
                <ProductAvatar name={group.name} image={group.image} categoryName={group.categoryName} />
              </div>
              <div className="flex-1 flex flex-col gap-1 text-left">
                <div className="flex items-center justify-between">
                  <h3 className="font-bold text-foreground text-base tracking-tight">
                    {group.name} (x{group.totalQuantity} {t("pantry.total")})
                  </h3>
                  <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-200 group-data-[state=open]:rotate-180" />
                </div>
                <p className="text-[11px] text-muted-foreground flex items-center gap-1 font-medium">
                  <Clock className="h-3 w-3" />
                  {t("pantry.expires", { date: group.earliestExpiry })} &bull; {categoryLabel(group.categoryName, t)}
                </p>
                <div className="mt-2 flex items-center gap-3">
                  <div className="h-1.5 flex-1 rounded-full bg-muted overflow-hidden">
                    <div className={cn("h-full rounded-full transition-all duration-1000", getStatusColor(group.worstStatus))} style={{ width: progressWidth(group.earliestDays) }} />
                  </div>
                  <span className={cn("text-[10px] font-bold uppercase tracking-wider", statusTextColor(group.worstStatus))}>
                    {statusLabel(group.worstStatus, group.earliestDays, t)}
                  </span>
                </div>
              </div>
            </div>
          </AccordionTrigger>
          <AccordionContent className="pb-0 pt-0">
            <div className="flex flex-col gap-2 px-4 pb-4">
              {group.enrichedBatches.map((batch) => (
                <BatchSubCard key={batch.batchId} batch={batch} onDelete={onDelete} onEdit={(b) => onEdit(group, b)} isDeleting={isDeleting} t={t} />
              ))}
              <div className="pt-2 border-t border-border">
                {confirmDeleteAll ? (
                  <div className="flex gap-2">
                    <button
                      className="flex-1 h-9 rounded-xl bg-destructive text-destructive-foreground text-xs font-bold flex items-center justify-center gap-1.5 active:scale-95 disabled:opacity-50"
                      disabled={isDeletingProduct}
                      onClick={() => { onDeleteProduct(group.productId); setConfirmDeleteAll(false); }}
                    >
                      {isDeletingProduct ? t("pantry.deleting") : t("pantry.confirmDeleteAll")}
                    </button>
                    <button
                      className="flex-1 h-9 rounded-xl bg-muted text-muted-foreground text-xs font-bold flex items-center justify-center active:scale-95"
                      onClick={() => setConfirmDeleteAll(false)}
                    >
                      {t("pantry.cancel")}
                    </button>
                  </div>
                ) : (
                  <button
                    className="w-full h-9 rounded-xl bg-red-500/10 text-red-600 dark:text-red-400 text-xs font-bold flex items-center justify-center gap-1.5 hover:bg-red-500/20 active:scale-95 transition-all"
                    onClick={() => setConfirmDeleteAll(true)}
                  >
                    <Trash2 className="h-3.5 w-3.5" /> {t("pantry.deleteAllBatches")}
                  </button>
                )}
              </div>
            </div>
          </AccordionContent>
        </div>
      </AccordionItem>
    </Accordion>
  );
};

const ProductGroupCard = ({ group, onDelete, onDeleteProduct, onEdit, isDeleting, isDeletingProduct, t }: { group: ProductGroup; onDelete: (batchId: string) => void; onDeleteProduct: (productId: string) => void; onEdit: (group: ProductGroup, batch: EnrichedBatch) => void; isDeleting: boolean; isDeletingProduct: boolean; t: TFunc }) => {
  if (group.enrichedBatches.length === 1) {
    return <SingleBatchCard group={group} onDeleteProduct={onDeleteProduct} onEdit={onEdit} isDeletingProduct={isDeletingProduct} t={t} />;
  }
  return <MultiBatchCard group={group} onDelete={onDelete} onDeleteProduct={onDeleteProduct} onEdit={onEdit} isDeleting={isDeleting} isDeletingProduct={isDeletingProduct} t={t} />;
};

// ---------- Main Page ----------

type EditState = {
  batchId: string;
  name: string;
  categoryName: string;
  expiryDate: string;
  quantity: number;
  alertTiming: string;
  productImage: string | null;
} | null;

export default function Pantry() {
  const { t } = useTranslation();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | "expiring" | "expired" | "safe">("all");
  const [selectedCategory, setSelectedCategory] = useState<string>(ALL_CATEGORIES);
  const [editState, setEditState] = useState<EditState>(null);
  const [editSaving, setEditSaving] = useState(false);
  const { token } = useAuth();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const highlightId = searchParams.get("highlight");
  const [highlightedId, setHighlightedId] = useState<string | null>(null);

  // Scroll to and highlight the product from notification
  useEffect(() => {
    if (highlightId) {
      setHighlightedId(highlightId);
      // Clear the param from URL without navigation
      setSearchParams({}, { replace: true });
      // Scroll to the element after a brief render delay
      setTimeout(() => {
        const el = document.getElementById(`product-${highlightId}`);
        if (el) {
          el.scrollIntoView({ behavior: "smooth", block: "center" });
        }
      }, 300);
      // Remove highlight after 3 seconds
      const timer = setTimeout(() => setHighlightedId(null), 3000);
      return () => clearTimeout(timer);
    }
  }, [highlightId]);

  const ALERT_OPTIONS = [
    { value: "7d", label: t("alert.7daysBefore") },
    { value: "3d", label: t("alert.3daysBefore") },
    { value: "1d", label: t("alert.1dayBefore") },
    { value: "on_expiry", label: t("alert.onExpiryDay") },
    { value: "none", label: t("alert.noAlert") },
  ];

  const invalidateAll = () => {
    qc.invalidateQueries({ queryKey: ["pantry"] });
    qc.invalidateQueries({ queryKey: ["dashboard"] });
    qc.invalidateQueries({ queryKey: ["recipes"] });
  };

  const pantryQuery = useQuery({
    queryKey: ["pantry"],
    queryFn: () => api.products(token!),
    enabled: !!token,
  });

  const categories = useMemo(() => {
    const cats = (pantryQuery.data?.items ?? []).map((p) => p.categoryName).filter(Boolean);
    return [ALL_CATEGORIES, ...Array.from(new Set(cats))];
  }, [pantryQuery.data]);

  const deleteBatch = useMutation({
    mutationFn: (batchId: string) => {
      if (!token) throw new Error("Authentication required.");
      return api.deleteBatch(token, batchId);
    },
    onSuccess: () => {
      invalidateAll();
      toast.success(t("pantry.batchDeleted"));
    },
    onError: (err) => {
      toast.error(err instanceof Error ? err.message : t("pantry.failedToDelete"));
    },
  });

  const deleteProduct = useMutation({
    mutationFn: (productId: string) => {
      if (!token) throw new Error("Authentication required.");
      return api.deleteProduct(token, productId);
    },
    onSuccess: () => {
      invalidateAll();
      toast.success(t("pantry.itemDeleted"));
    },
    onError: (err) => {
      toast.error(err instanceof Error ? err.message : t("pantry.failedToDeleteItem"));
    },
  });

  const editImageRef = useRef<HTMLInputElement>(null);

  const openEdit = (group: ProductGroup, batch: EnrichedBatch) => {
    // Try to load saved product image from localStorage
    let savedImage: string | null = null;
    try {
      const images: Record<string, string> = JSON.parse(localStorage.getItem("foodtrack_product_images") || "{}");
      savedImage = images[group.name.toLowerCase().trim()] || null;
    } catch { /* ignore */ }

    setEditState({
      batchId: batch.batchId,
      name: group.name,
      categoryName: group.categoryName,
      expiryDate: batch.expiryDate,
      quantity: batch.quantity,
      alertTiming: getProductAlertTiming(group.name),
      productImage: savedImage,
    });
  };

  const onEditImageChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !editState) return;
    const reader = new FileReader();
    reader.onload = () => {
      setEditState((s) => s ? { ...s, productImage: reader.result as string } : s);
    };
    reader.readAsDataURL(file);
    e.target.value = "";
  };

  const handleEditNameChange = (value: string) => {
    setEditState((s) => {
      if (!s) return s;
      const cat = inferCategory(value);
      return { ...s, name: value, categoryName: cat !== "General" ? cat : s.categoryName };
    });
  };

  const saveEdit = async () => {
    if (!editState || !token) return;
    setEditSaving(true);
    try {
      await api.deleteBatch(token, editState.batchId);
      await api.addProduct(token, {
        name: editState.name,
        categoryName: editState.categoryName,
        expiryDate: editState.expiryDate,
        quantity: editState.quantity,
      });
      saveProductAlert(editState.name, editState.expiryDate, editState.alertTiming);
      // Save product image if provided
      if (editState.productImage) {
        try {
          const images: Record<string, string> = JSON.parse(localStorage.getItem("foodtrack_product_images") || "{}");
          images[editState.name.toLowerCase().trim()] = editState.productImage;
          localStorage.setItem("foodtrack_product_images", JSON.stringify(images));
        } catch { /* ignore */ }
      }
      invalidateAll();
      setEditState(null);
      toast.success(t("pantry.itemUpdated"));
    } catch {
      // keep sheet open on error
    } finally {
      setEditSaving(false);
    }
  };

  const items = useMemo(() => {
    const groups = groupProducts(pantryQuery.data?.items ?? []);
    return groups.filter((g) => {
      const matchSearch = g.name.toLowerCase().includes(search.toLowerCase());
      const matchStatus = statusFilter === "all" || g.enrichedBatches.some((b) => b.status === statusFilter);
      const matchCategory = selectedCategory === ALL_CATEGORIES || g.categoryName === selectedCategory;
      return matchSearch && matchStatus && matchCategory;
    });
  }, [pantryQuery.data, search, statusFilter, selectedCategory]);

  const filters: { label: string; value: "all" | "expiring" | "expired" | "safe" }[] = [
    { label: t("pantry.filterAll"), value: "all" },
    { label: t("pantry.filterExpiring"), value: "expiring" },
    { label: t("pantry.filterExpired"), value: "expired" },
    { label: t("pantry.filterSafe"), value: "safe" },
  ];

  return (
    <div className="flex flex-col gap-6 p-6 pb-40 h-full animate-in fade-in duration-500">
      <div className="sticky top-0 z-20 bg-background/90 backdrop-blur-sm pt-1 pb-2">
        <div className="flex gap-2">
          <div className="relative flex-1 group">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground group-focus-within:text-primary transition-colors" />
            <Input
              placeholder={selectedCategory === ALL_CATEGORIES ? t("pantry.searchPlaceholder") : t("pantry.searchCategoryPlaceholder", { category: categoryLabel(selectedCategory, t) })}
              className="pl-11 h-12 rounded-2xl border-border bg-card shadow-sm focus:ring-primary/20"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" className={cn(
                "h-12 w-12 rounded-2xl p-0 border-border bg-card shadow-sm active:scale-95 transition-all text-muted-foreground hover:bg-muted/50 focus-visible:ring-primary/20 focus-visible:outline-none focus-visible:ring-2",
                selectedCategory !== ALL_CATEGORIES && "bg-primary/10 border-primary/20 text-primary"
              )}>
                <Filter className={cn("h-5 w-5", selectedCategory !== ALL_CATEGORIES && "fill-primary/20")} />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56 rounded-2xl border-border shadow-xl p-2 bg-card/80 backdrop-blur-xl">
              <DropdownMenuLabel className="font-bold text-xs uppercase tracking-wider text-muted-foreground mb-1 ml-2">{t("pantry.filterByCategory")}</DropdownMenuLabel>
              {categories.map((category) => (
                <DropdownMenuItem
                  key={category}
                  onClick={() => setSelectedCategory(category)}
                  className="rounded-xl cursor-pointer py-2.5 px-3 font-medium text-sm flex items-center justify-between group hover:bg-muted focus:bg-muted/50"
                >
                  {category === ALL_CATEGORIES ? t("pantry.allCategories") : categoryLabel(category, t)}
                  {selectedCategory === category && <Check className="h-4 w-4 text-primary animate-in zoom-in" />}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

        <div className="flex gap-2 overflow-x-auto no-scrollbar pb-1 mt-3">
          {filters.map((filter) => (
            <Badge
              key={filter.value}
              onClick={() => setStatusFilter(filter.value)}
              variant={statusFilter === filter.value ? "default" : "outline"}
              className={cn(
                "rounded-xl px-5 h-9 cursor-pointer transition-colors active:scale-95",
                statusFilter === filter.value ? "font-bold shadow-md" : "bg-card border-border text-muted-foreground font-semibold hover:bg-muted/50"
              )}
            >
              {filter.label}
            </Badge>
          ))}
        </div>
      </div>

      <div className="flex flex-col gap-4 mt-2">
        {pantryQuery.isLoading && <p className="text-sm text-muted-foreground">{t("pantry.loading")}</p>}
        {!pantryQuery.isLoading && items.length === 0 && (
          <div className="py-12 flex flex-col items-center justify-center text-center gap-2">
            <div className="w-16 h-16 rounded-full bg-muted/50 flex items-center justify-center mb-2">
              <Search className="h-8 w-8 text-muted-foreground/50" />
            </div>
            <h3 className="font-bold text-foreground">{t("pantry.noItemsFound")}</h3>
            <p className="text-sm text-muted-foreground">{t("pantry.noItemsHint")}</p>
          </div>
        )}
        {items.map((group) => (
          <div
            key={group.productId}
            id={`product-${group.productId}`}
            className={cn(
              "transition-all duration-500",
              highlightedId === group.productId && "ring-2 ring-primary ring-offset-2 rounded-[2rem] bg-primary/5"
            )}
          >
            <ProductGroupCard
              group={group}
              onDelete={(id) => deleteBatch.mutate(id)}
              onDeleteProduct={(id) => deleteProduct.mutate(id)}
              onEdit={openEdit}
              isDeleting={deleteBatch.isPending}
              isDeletingProduct={deleteProduct.isPending}
              t={t}
            />
          </div>
        ))}
      </div>

      <Button onClick={() => navigate("/scan?source=pantry&mode=manual")} className="fixed bottom-24 right-6 w-14 h-14 rounded-2xl shadow-xl shadow-primary/30 z-50 animate-in slide-in-from-bottom-10">
        <Plus className="h-6 w-6" />
      </Button>

      {editState && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-[60] flex items-center justify-center p-6 animate-in fade-in duration-200" onClick={() => setEditState(null)}>
          <div className="w-full max-w-sm bg-card rounded-[2rem] p-6 flex flex-col gap-4 shadow-2xl animate-in zoom-in-95 duration-300 max-h-[85vh] overflow-y-auto no-scrollbar" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-bold text-foreground">{t("pantry.editItem")}</h3>
              <button onClick={() => setEditState(null)} className="w-8 h-8 rounded-full bg-muted flex items-center justify-center">
                <X className="h-4 w-4 text-muted-foreground" />
              </button>
            </div>

            {/* Product image upload */}
            <div className="flex flex-col items-center gap-2">
              <button
                onClick={() => editImageRef.current?.click()}
                className="w-24 h-24 rounded-2xl border-2 border-dashed border-border bg-muted/30 flex flex-col items-center justify-center gap-1 hover:bg-muted/50 transition-colors overflow-hidden"
              >
                {editState.productImage ? (
                  <img src={editState.productImage} alt="product" className="w-full h-full object-cover" />
                ) : (
                  <>
                    <Upload className="h-6 w-6 text-muted-foreground" />
                    <span className="text-[10px] text-muted-foreground font-medium">{t("pantry.addPhoto")}</span>
                  </>
                )}
              </button>
              {editState.productImage && (
                <button onClick={() => setEditState((s) => s ? { ...s, productImage: null } : s)} className="text-[10px] text-red-500 font-medium">
                  {t("pantry.removePhoto")}
                </button>
              )}
              <input ref={editImageRef} type="file" accept="image/*" className="hidden" onChange={onEditImageChange} />
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium mb-1 block">{t("pantry.productName")}</label>
              <Input
                value={editState.name}
                onChange={(e) => handleEditNameChange(e.target.value)}
                placeholder={t("pantry.productNamePlaceholder")}
                className="rounded-xl h-11"
              />
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium mb-1 block">{t("pantry.categoryLabel")}</label>
              <select
                className="h-11 rounded-xl border border-input px-3 text-sm bg-background text-foreground w-full"
                value={editState.categoryName}
                onChange={(e) => setEditState((s) => s ? { ...s, categoryName: e.target.value } : s)}
              >
                {CATEGORIES.map((cat) => <option key={cat} value={cat}>{categoryLabel(cat, t)}</option>)}
              </select>
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium mb-1 block">{t("pantry.expiryDate")}</label>
              <Input
                type="date"
                value={editState.expiryDate}
                onChange={(e) => setEditState((s) => s ? { ...s, expiryDate: e.target.value } : s)}
                className="rounded-xl h-11"
              />
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium mb-1 block">{t("pantry.quantity")}</label>
              <Input
                type="number"
                min={1}
                value={editState.quantity}
                onChange={(e) => setEditState((s) => s ? { ...s, quantity: Number(e.target.value) || 1 } : s)}
                className="rounded-xl h-11"
              />
            </div>

            <div>
              <label className="text-[10px] text-muted-foreground font-medium flex items-center gap-1 mb-1.5">
                <Bell className="h-3 w-3" /> {t("pantry.expiryAlert")}
              </label>
              <div className="flex flex-wrap gap-1.5">
                {ALERT_OPTIONS.map((opt) => (
                  <button
                    key={opt.value}
                    onClick={() => setEditState((s) => s ? { ...s, alertTiming: opt.value } : s)}
                    className={`px-3 py-1.5 rounded-full text-[11px] font-semibold border transition-colors ${
                      editState.alertTiming === opt.value
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
              onClick={saveEdit}
              disabled={editSaving || !editState.name.trim()}
              className="rounded-2xl h-12 font-bold"
            >
              {editSaving ? t("pantry.saving") : t("pantry.saveChanges")}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
