import { Bell, UtensilsCrossed, AlertTriangle, Clock, X } from "lucide-react";
import { useLocation, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useState, useRef, useEffect, useCallback } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { api, type NotificationItem } from "@/lib/api";
import { playNotificationSound } from "@/lib/notificationSounds";
import { useTranslation } from "@/lib/i18n/LanguageContext";

// ── WhatsApp-style in-app toast ─────────────────────────────────────────
type ToastNotif = { id: string; title: string; body: string; productId?: string | null };

function NotificationToast({ notif, onDismiss, onTap }: { notif: ToastNotif; onDismiss: () => void; onTap: () => void }) {
  useEffect(() => {
    const timer = setTimeout(onDismiss, 5000);
    return () => clearTimeout(timer);
  }, [onDismiss]);

  return (
    <div
      onClick={onTap}
      className="animate-in slide-in-from-top-2 fade-in duration-300 cursor-pointer w-[calc(100%-2rem)] max-w-sm mx-auto bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl border border-border/50 p-3 flex gap-3 items-start"
    >
      <div className="flex-shrink-0 w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center">
        <Bell className="h-5 w-5 text-primary" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-bold text-foreground truncate">{notif.title}</p>
        <p className="text-xs text-muted-foreground line-clamp-2 mt-0.5">{notif.body}</p>
        <p className="text-[10px] text-muted-foreground/60 mt-1">just now</p>
      </div>
      <button
        onClick={(e) => { e.stopPropagation(); onDismiss(); }}
        className="flex-shrink-0 p-1 rounded-full hover:bg-muted/50"
      >
        <X className="h-3.5 w-3.5 text-muted-foreground" />
      </button>
    </div>
  );
}

// ── Main TopNav ─────────────────────────────────────────────────────────

export function TopNav() {
  const location = useLocation();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const { token } = useAuth();
  const { t } = useTranslation();
  const dropdownRef = useRef<HTMLDivElement>(null);
  const qc = useQueryClient();
  const [toasts, setToasts] = useState<ToastNotif[]>([]);
  const [clearing, setClearing] = useState(false);
  const [readIds, setReadIds] = useState<Set<string>>(() => {
    try {
      const raw = localStorage.getItem("foodtrack_read_notifs");
      return raw ? new Set(JSON.parse(raw)) : new Set();
    } catch { return new Set(); }
  });

  const titles: Record<string, string> = {
    "/": t("nav.topTitle.default"),
    "/pantry": t("nav.topTitle.pantry"),
    "/scan": t("nav.topTitle.scanner"),
    "/recipes": t("nav.topTitle.recipes"),
    "/recipes/": t("nav.topTitle.recipes"),
    "/profile": t("nav.topTitle.profile"),
  };

  const currentTitle = titles[location.pathname] || t("nav.topTitle.default");

  function timeAgo(dateStr: string): string {
    const now = Date.now();
    const then = new Date(dateStr).getTime();
    const diffMs = now - then;
    const mins = Math.floor(diffMs / 60000);
    if (mins < 1) return t("notifications.justNow");
    if (mins < 60) return t("notifications.minutesAgo", { count: mins });
    const hours = Math.floor(mins / 60);
    if (hours < 24) return t("notifications.hoursAgo", { count: hours });
    const days = Math.floor(hours / 24);
    if (days === 1) return t("notifications.yesterday");
    return t("notifications.daysAgo", { count: days });
  }

  function notificationLabel(type: string): string {
    if (type === "EXPIRY_7_DAYS") return t("notifications.expiresIn7Days");
    if (type === "EXPIRY_3_DAYS") return t("notifications.expiresIn3Days");
    if (type === "EXPIRY_1_DAY") return t("notifications.expiresTomorrow");
    if (type === "EXPIRY_TODAY") return t("notifications.expiresToday");
    if (type === "TEST") return t("notifications.testNotification");
    return t("notifications.expiryAlert");
  }

  function notificationIcon(type: string) {
    if (type === "EXPIRY_TODAY") return <AlertTriangle className="h-4 w-4 text-red-500" />;
    if (type === "EXPIRY_1_DAY") return <AlertTriangle className="h-4 w-4 text-orange-500" />;
    if (type === "EXPIRY_3_DAYS") return <Clock className="h-4 w-4 text-yellow-500" />;
    return <Bell className="h-4 w-4 text-primary" />;
  }

  // Close dropdown when clicking outside
  const handleClickOutside = useCallback((e: MouseEvent) => {
    if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
      setOpen(false);
    }
  }, []);

  useEffect(() => {
    if (open) {
      document.addEventListener("mousedown", handleClickOutside);
      return () => document.removeEventListener("mousedown", handleClickOutside);
    }
  }, [open, handleClickOutside]);

  // Mark all as read when dropdown is opened
  useEffect(() => {
    if (open && notifications.length > 0) {
      const allIds = new Set(notifications.map((n) => n.id));
      setReadIds(allIds);
      localStorage.setItem("foodtrack_read_notifs", JSON.stringify([...allIds]));
    }
  }, [open]);

  const prevNotifIdsRef = useRef<Set<string>>(new Set());
  const hasInteracted = useRef(false);

  // Request browser notification permission on first interaction
  useEffect(() => {
    const markInteracted = () => {
      hasInteracted.current = true;
      if ("Notification" in window && Notification.permission === "default") {
        Notification.requestPermission();
      }
    };
    window.addEventListener("click", markInteracted, { once: true });
    window.addEventListener("touchstart", markInteracted, { once: true });
    return () => {
      window.removeEventListener("click", markInteracted);
      window.removeEventListener("touchstart", markInteracted);
    };
  }, []);

  const notificationsQuery = useQuery({
    queryKey: ["notifications"],
    queryFn: () => api.notifications(token!),
    enabled: !!token,
    refetchInterval: 10_000,
    refetchIntervalInBackground: true,
  });

  const notifications = notificationsQuery.data ?? [];

  // Unread = recent (last 24h) + success + not in readIds
  const unreadCount = notifications.filter((n) => {
    const diff = Date.now() - new Date(n.sentAtUtc).getTime();
    return diff < 24 * 60 * 60 * 1000 && n.success && !readIds.has(n.id);
  }).length;

  useEffect(() => {
    if (prevNotifIdsRef.current.size === 0) {
      prevNotifIdsRef.current = new Set(notifications.map((n) => n.id));
      return;
    }

    const newNotifs = notifications.filter(
      (n) => n.success && !prevNotifIdsRef.current.has(n.id)
    );

    if (newNotifs.length > 0 && hasInteracted.current) {
      playNotificationSound();

      // Show WhatsApp-style in-app toasts
      const newToasts: ToastNotif[] = newNotifs.map((n) => ({
        id: n.id,
        title: n.title || notificationLabel(n.notificationType),
        body: n.body || notificationLabel(n.notificationType),
        productId: n.productId,
      }));
      setToasts((prev) => [...newToasts, ...prev].slice(0, 3));

      // Browser push notification
      if ("Notification" in window && Notification.permission === "granted") {
        for (const n of newNotifs) {
          const title = n.title || notificationLabel(n.notificationType);
          const body = n.body || notificationLabel(n.notificationType);
          try {
            new Notification(title, {
              body,
              icon: "/icons/icon-192x192.png",
              badge: "/icons/icon-192x192.png",
              tag: n.id,
              requireInteraction: false,
            });
          } catch { }
        }
      }
    }

    prevNotifIdsRef.current = new Set(notifications.map((n) => n.id));
  }, [notifications]);

  const dismissToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const goToProduct = useCallback((productId?: string | null) => {
    setOpen(false);
    setToasts([]);
    if (productId) {
      navigate(`/pantry?highlight=${productId}`);
    } else {
      navigate("/pantry");
    }
  }, [navigate]);

  return (
    <>
      {/* WhatsApp-style floating toast notifications */}
      {toasts.length > 0 && (
        <div className="fixed top-2 left-0 right-0 z-[100] flex flex-col gap-2 items-center pointer-events-none">
          {toasts.map((toast) => (
            <div key={toast.id} className="pointer-events-auto">
              <NotificationToast
                notif={toast}
                onDismiss={() => dismissToast(toast.id)}
                onTap={() => goToProduct(toast.productId)}
              />
            </div>
          ))}
        </div>
      )}

      <header className="sticky top-0 z-40 w-full bg-background/80 backdrop-blur-lg border-b border-border px-6 h-20 flex items-end pb-4 justify-between">
        <div className="flex items-center gap-2">
          <UtensilsCrossed className="h-6 w-6 text-primary" />
          <h1 className="text-xl font-bold text-foreground tracking-tight">{currentTitle}</h1>
        </div>

        <div className="relative" ref={dropdownRef}>
          <Button variant="ghost" size="icon" className="rounded-full h-10 w-10 relative" onClick={() => setOpen((v) => !v)}>
            <Bell className="h-5 w-5 text-muted-foreground" />
            {unreadCount > 0 && (
              <span className="absolute -top-0.5 -right-0.5 min-w-5 h-5 px-1 bg-red-500 rounded-full border-2 border-background flex items-center justify-center">
                <span className="text-[10px] font-bold text-white">{unreadCount > 9 ? "9+" : unreadCount}</span>
              </span>
            )}
          </Button>

          {open && (
            <div className="absolute right-0 mt-2 w-80 max-h-96 overflow-auto rounded-2xl border border-border bg-white dark:bg-card shadow-xl p-3 z-50">
              <div className="flex items-center justify-between mb-3">
                <p className="text-sm font-bold">{t("notifications.title")}</p>
                {notifications.length > 0 && (
                  <button
                    disabled={clearing}
                    onClick={async (e) => {
                      e.stopPropagation();
                      if (!token) return;
                      setClearing(true);
                      try {
                        await api.clearNotifications(token);
                        qc.invalidateQueries({ queryKey: ["notifications"] });
                        setReadIds(new Set());
                        localStorage.removeItem("foodtrack_read_notifs");
                      } catch { }
                      setClearing(false);
                    }}
                    className="text-[10px] font-semibold text-red-500 hover:text-red-600 active:scale-95 transition-all disabled:opacity-50"
                  >
                    {clearing ? "..." : "Clear all"}
                  </button>
                )}
              </div>
              {notificationsQuery.isLoading && <p className="text-xs text-muted-foreground">{t("notifications.loading")}</p>}
              {!notificationsQuery.isLoading && notifications.length === 0 && (
                <div className="py-8 text-center">
                  <Bell className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                  <p className="text-xs text-muted-foreground">{t("notifications.empty")}</p>
                </div>
              )}
              <div className="flex flex-col gap-2">
                {notifications.map((n) => (
                  <div
                    key={n.id}
                    onClick={() => goToProduct(n.productId)}
                    className={`rounded-xl border p-3 text-xs flex gap-2.5 items-start cursor-pointer hover:bg-muted/30 active:scale-[0.98] transition-all ${n.success ? "" : "opacity-50"}`}
                  >
                    <div className="flex-shrink-0 w-8 h-8 rounded-full bg-muted/50 flex items-center justify-center mt-0.5">
                      {notificationIcon(n.notificationType)}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center justify-between gap-2">
                        <p className="font-bold text-foreground truncate">{n.title || notificationLabel(n.notificationType)}</p>
                        <span className="text-[10px] text-muted-foreground flex-shrink-0">{timeAgo(n.sentAtUtc)}</span>
                      </div>
                      <p className="text-muted-foreground mt-0.5">{n.body || notificationLabel(n.notificationType)}</p>
                      {!n.success && <p className="text-red-500 text-[10px] mt-0.5">{n.errorMessage ?? t("notifications.failedToDeliver")}</p>}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </header>
    </>
  );
}
