export type AuthResponse = {
  userId: string;
  email: string;
  accessToken: string;
};

export type Dashboard = {
  totalProducts: number;
  expiringSoonCount: number;
  usedThisMonth: number;
  wasteThisMonth: number;
};

export type Batch = {
  batchId: string;
  expiryDate: string;
  quantity: number;
  status: number;
};

export type Product = {
  productId: string;
  name: string;
  categoryName: string;
  totalQuantity: number;
  batches: Batch[];
};

export type PagedProducts = {
  page: number;
  pageSize: number;
  totalCount: number;
  items: Product[];
};

export type RecipeSuggestion = {
  recipeId: string;
  name: string;
  mealType: string;
  region: string;
  imageUrl: string;
  ingredients: string[];
  steps: string[];
  matchPercent: number;
  expiryPriorityScore: number;
  finalScore: number;
};

export type FieldConfidence = {
  nameConfidence: string;
  expiryConfidence: string;
  categoryConfidence: string;
};

export type OcrExtracted = {
  productName: string;
  manufacturingDate?: string | null;
  expiryDate: string;
  daysLeftToExpire: number;
  categoryName: string;
  isConfidenceLow: boolean;
  productCandidates?: string[] | null;
  confidenceScore?: number;
  fieldConfidence?: FieldConfidence | null;
  needsHumanReview?: boolean;
};

export type DetectedItemResult = {
  productName: string;
  categoryName: string;
  expiryDate: string;
  daysLeftToExpire: number;
  confidenceScore: number;
  needsHumanReview?: boolean;
};

export type OcrImagePreviewResponse = {
  extracted: OcrExtracted;
  rawText: string;
  detectedItems?: DetectedItemResult[] | null;
};

export type OcrImageResponse = {
  extracted: OcrExtracted;
  inventoryProduct: Product;
  rawText: string;
};

export type ProfileDto = {
  userId: string;
  email: string;
  role: string;
  firstName: string;
  lastName: string;
  age?: number | null;
  profilePhotoDataUrl?: string | null;
};

export type NotificationItem = {
  id: string;
  notificationType: string;
  sentAtUtc: string;
  success: boolean;
  errorMessage?: string | null;
};

const API_BASE = import.meta.env.VITE_API_BASE_URL || "";

function buildUrl(path: string) {
  return `${API_BASE}${path}`;
}

async function request<T>(path: string, init: RequestInit = {}, token?: string): Promise<T> {
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && !(init.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  let response: Response;
  try {
    response = await fetch(buildUrl(path), { ...init, headers });
  } catch {
    throw new Error(`Cannot reach backend API. Make sure backend is running on ${API_BASE || "http://127.0.0.1:5001"}.`);
  }

  if (!response.ok) {
    let message = `Request failed (${response.status})`;
    try {
      const payload = await response.json();
      if (payload?.error) message = payload.error;
    } catch {
      // ignore
    }
    throw new Error(message);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

function makeForm(file: Blob | File, fileName: string, quantity: number) {
  const form = new FormData();
  form.append("image", file, fileName);
  form.append("quantity", String(quantity));
  return form;
}

function makeFrontBackForm(front: Blob | File, frontName: string, back: Blob | File, backName: string, quantity: number) {
  const form = new FormData();
  form.append("frontImage", front, frontName);
  form.append("backImage", back, backName);
  form.append("quantity", String(quantity));
  return form;
}

function makeMultiImageForm(images: Array<{ file: Blob | File; fileName: string }>, quantity: number) {
  const form = new FormData();
  for (const image of images) {
    form.append("images", image.file, image.fileName);
  }
  form.append("quantity", String(quantity));
  return form;
}

export const api = {
  register: (body: { email: string; password: string; confirmPassword: string; firstName: string; lastName: string; age?: number | null }) =>
    request<AuthResponse>("/api/v1/auth/register", {
      method: "POST",
      body: JSON.stringify(body),
    }),

  login: (email: string, password: string) =>
    request<AuthResponse>("/api/v1/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    }),

  dashboard: (token: string) => request<Dashboard>("/api/v1/dashboard", {}, token),

  products: (token: string, category?: string) =>
    request<PagedProducts>(`/api/v1/products?page=1&pageSize=100${category ? `&category=${encodeURIComponent(category)}` : ""}`, {}, token),

  addProduct: (token: string, body: { name: string; categoryName: string; expiryDate: string; quantity: number }) =>
    request<Product>("/api/v1/products", { method: "POST", body: JSON.stringify(body) }, token),

  consumeBatch: (token: string, batchId: string, quantityUsed = 1) =>
    request<void>("/api/v1/products/consume", {
      method: "POST",
      body: JSON.stringify({ batchId, quantityUsed }),
    }, token),

  recipes: (token: string) => request<RecipeSuggestion[]>("/api/v1/recipes/suggestions", {}, token),

  scanImagePreview: (token: string, file: Blob | File, fileName: string, quantity: number) =>
    request<OcrImagePreviewResponse>("/api/v1/ocr/scan-image-preview", { method: "POST", body: makeForm(file, fileName, quantity) }, token),

  scanImage: (token: string, file: Blob | File, fileName: string, quantity: number) =>
    request<OcrImageResponse>("/api/v1/ocr/scan-image", { method: "POST", body: makeForm(file, fileName, quantity) }, token),

  scanFrontBackPreview: (token: string, front: Blob | File, frontName: string, back: Blob | File, backName: string, quantity: number) =>
    request<OcrImagePreviewResponse>("/api/v1/ocr/scan-image-preview", { method: "POST", body: makeFrontBackForm(front, frontName, back, backName, quantity) }, token),

  scanFrontBack: (token: string, front: Blob | File, frontName: string, back: Blob | File, backName: string, quantity: number) =>
    request<OcrImageResponse>("/api/v1/ocr/scan-image", { method: "POST", body: makeFrontBackForm(front, frontName, back, backName, quantity) }, token),

  scanMultiPreview: (token: string, images: Array<{ file: Blob | File; fileName: string }>, quantity: number) =>
    request<OcrImagePreviewResponse>("/api/v1/ocr/scan-image-preview", { method: "POST", body: makeMultiImageForm(images, quantity) }, token),

  scanMulti: (token: string, images: Array<{ file: Blob | File; fileName: string }>, quantity: number) =>
    request<OcrImageResponse>("/api/v1/ocr/scan-image", { method: "POST", body: makeMultiImageForm(images, quantity) }, token),

  runNotifications: (token: string) => request<void>("/api/v1/notifications/run-daily-job", { method: "POST" }, token),
  notifications: (token: string) => request<NotificationItem[]>("/api/v1/notifications/history", {}, token),

  profile: (token: string) => request<ProfileDto>("/api/v1/profile/me", {}, token),

  updateProfile: (token: string, body: { firstName: string; lastName: string; age?: number | null; profilePhotoDataUrl?: string | null }) =>
    request<void>("/api/v1/profile", {
      method: "PUT",
      body: JSON.stringify(body),
    }, token),

  changePassword: (token: string, currentPassword: string, newPassword: string) =>
    request<void>("/api/v1/profile/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword, newPassword }),
    }, token),

  logout: (token: string) => request<void>("/api/v1/profile/logout", { method: "POST" }, token),

  deleteAccount: (token: string) => request<void>("/api/v1/profile", { method: "DELETE" }, token),

  registerDevice: (token: string, fcmToken: string, platform = "android") =>
    request<{ message: string }>("/api/v1/notifications/register-device", {
      method: "POST",
      body: JSON.stringify({ token: fcmToken, platform }),
    }, token),
};
