import { Capacitor } from "@capacitor/core";
import { LocalNotifications } from "@capacitor/local-notifications";
import type { Product } from "./api";

// ── Types ────────────────────────────────────────────────────────────────

export type InAppNotification = {
  id: string;
  title: string;
  body: string;
  type: "expiring" | "expired";
  productName: string;
  batchId: string;
  createdAt: string; // ISO
  read: boolean;
};

export type Preferences = {
  enableAlerts: boolean;
  silentHours: boolean;
  silentStart: string;
  silentEnd: string;
  reminderTiming: "7 Days" | "3 Days" | "1 Day" | "On Expiry";
  notificationTime: string; // "HH:mm"
};

const HISTORY_KEY = "foodtrack_notification_history";
const MAX_HISTORY = 50;
const ANDROID_NOTIF_CAP = 64;

// ── Deterministic ID (positive 32-bit int) ───────────────────────────────

export function stableNotifId(batchId: string, type: string): number {
  const str = `${batchId}_${type}`;
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    hash = (hash * 31 + str.charCodeAt(i)) | 0;
  }
  return (hash & 0x7fffffff) || 1; // ensure positive, non-zero
}

// ── Reminder offset in days ──────────────────────────────────────────────

function reminderDays(timing: Preferences["reminderTiming"]): number {
  switch (timing) {
    case "7 Days": return 7;
    case "3 Days": return 3;
    case "1 Day":  return 1;
    case "On Expiry": return 0;
  }
}

// ── Silent-hours check ───────────────────────────────────────────────────

function isDuringSilentHours(date: Date, prefs: Preferences): boolean {
  if (!prefs.silentHours) return false;
  const hm = date.getHours() * 60 + date.getMinutes();
  const [sh, sm] = prefs.silentStart.split(":").map(Number);
  const [eh, em] = prefs.silentEnd.split(":").map(Number);
  const start = sh * 60 + sm;
  const end = eh * 60 + em;
  if (start <= end) return hm >= start && hm < end;
  return hm >= start || hm < end; // wraps midnight
}

// ── Schedule all local notifications ─────────────────────────────────────

export async function scheduleAllNotifications(products: Product[], prefs: Preferences) {
  // Always regenerate in-app notifications
  generateInAppNotifications(products);

  if (!prefs.enableAlerts) return;
  if (!Capacitor.isNativePlatform()) return;

  try {
    // Cancel all existing scheduled notifications
    const pending = await LocalNotifications.getPending();
    if (pending.notifications.length > 0) {
      await LocalNotifications.cancel({ notifications: pending.notifications });
    }
  } catch {
    // ignore cancel errors
  }

  const now = new Date();
  const [notifHour, notifMin] = (prefs.notificationTime || "09:00").split(":").map(Number);
  const offsetDays = reminderDays(prefs.reminderTiming);

  type NotifEntry = { id: number; title: string; body: string; at: Date };
  const entries: NotifEntry[] = [];

  for (const product of products) {
    for (const batch of product.batches) {
      if (batch.status !== 0) continue; // skip consumed/discarded
      const expiry = new Date(batch.expiryDate);

      // "Expiring soon" notification
      const expiringDate = new Date(expiry);
      expiringDate.setDate(expiringDate.getDate() - offsetDays);
      expiringDate.setHours(notifHour, notifMin, 0, 0);
      if (expiringDate > now && !isDuringSilentHours(expiringDate, prefs)) {
        const daysText = offsetDays === 0 ? "today" : offsetDays === 1 ? "tomorrow" : `in ${offsetDays} days`;
        entries.push({
          id: stableNotifId(batch.batchId, "expiring"),
          title: "Expiring Soon",
          body: `${product.name} expires ${daysText}! Use it before it goes to waste.`,
          at: expiringDate,
        });
      }

      // "Expired" notification (day after expiry)
      const expiredDate = new Date(expiry);
      expiredDate.setDate(expiredDate.getDate() + 1);
      expiredDate.setHours(notifHour, notifMin, 0, 0);
      if (expiredDate > now && !isDuringSilentHours(expiredDate, prefs)) {
        entries.push({
          id: stableNotifId(batch.batchId, "expired"),
          title: "Item Expired",
          body: `${product.name} has expired. Consider discarding or using it soon.`,
          at: expiredDate,
        });
      }
    }
  }

  // Sort by date, cap to Android limit
  entries.sort((a, b) => a.at.getTime() - b.at.getTime());
  const toSchedule = entries.slice(0, ANDROID_NOTIF_CAP);

  if (toSchedule.length > 0) {
    try {
      const perm = await LocalNotifications.requestPermissions();
      if (perm.display !== "granted") return;

      // Create notification channel (required for Android 8+)
      try {
        await LocalNotifications.createChannel({
          id: "foodtracker_alerts",
          name: "Food Expiry Alerts",
          description: "Notifications about expiring food items",
          importance: 5,
          sound: "default",
          vibration: true,
        });
      } catch {
        // Channel may already exist
      }

      await LocalNotifications.schedule({
        notifications: toSchedule.map((e) => ({
          id: e.id,
          title: e.title,
          body: e.body,
          schedule: { at: e.at },
          sound: "default",
          channelId: "foodtracker_alerts",
          smallIcon: "ic_launcher",
          largeIcon: "ic_launcher",
        })),
      });
    } catch (err) {
      console.error("Failed to schedule notifications:", err);
    }
  }
}

// ── In-app notifications (localStorage) ──────────────────────────────────

export function generateInAppNotifications(products: Product[]): void {
  const now = new Date();
  const existing = loadNotificationHistory();
  const existingKeys = new Set(existing.map((n) => `${n.batchId}_${n.type}`));
  const newNotifs: InAppNotification[] = [];

  for (const product of products) {
    for (const batch of product.batches) {
      if (batch.status !== 0) continue;
      const expiry = new Date(batch.expiryDate);
      const diffMs = expiry.getTime() - now.getTime();
      const diffDays = diffMs / (1000 * 60 * 60 * 24);

      if (diffDays < 0) {
        // Expired
        const key = `${batch.batchId}_expired`;
        if (!existingKeys.has(key)) {
          newNotifs.push({
            id: `${batch.batchId}_expired`,
            title: "Item Expired",
            body: `${product.name} has expired. Consider discarding or using it soon.`,
            type: "expired",
            productName: product.name,
            batchId: batch.batchId,
            createdAt: now.toISOString(),
            read: false,
          });
          existingKeys.add(key);
        }
      } else if (diffDays <= 2) {
        // Expiring soon (within 2 days)
        const key = `${batch.batchId}_expiring`;
        if (!existingKeys.has(key)) {
          const dayText = diffDays < 1 ? "today" : diffDays < 2 ? "tomorrow" : "in 2 days";
          newNotifs.push({
            id: `${batch.batchId}_expiring`,
            title: "Expiring Soon",
            body: `${product.name} expires ${dayText}! Use it before it goes to waste.`,
            type: "expiring",
            productName: product.name,
            batchId: batch.batchId,
            createdAt: now.toISOString(),
            read: false,
          });
          existingKeys.add(key);
        }
      }
    }
  }

  if (newNotifs.length > 0) {
    const merged = [...newNotifs, ...existing].slice(0, MAX_HISTORY);
    saveNotificationHistory(merged);
  }
}

// ── LocalStorage helpers ─────────────────────────────────────────────────

export function loadNotificationHistory(): InAppNotification[] {
  try {
    const raw = localStorage.getItem(HISTORY_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

export function saveNotificationHistory(items: InAppNotification[]): void {
  localStorage.setItem(HISTORY_KEY, JSON.stringify(items.slice(0, MAX_HISTORY)));
}

// ── Listeners (foreground local notifications) ───────────────────────────

let listenersSetup = false;
export function setupNotificationListeners(): void {
  if (listenersSetup || !Capacitor.isNativePlatform()) return;
  listenersSetup = true;

  LocalNotifications.addListener("localNotificationReceived", (notification) => {
    const history = loadNotificationHistory();
    const exists = history.some((n) => n.id === String(notification.id));
    if (!exists) {
      history.unshift({
        id: String(notification.id),
        title: notification.title ?? "Notification",
        body: notification.body ?? "",
        type: notification.title?.includes("Expired") ? "expired" : "expiring",
        productName: "",
        batchId: "",
        createdAt: new Date().toISOString(),
        read: false,
      });
      saveNotificationHistory(history);
    }
  });
}

// ── Unread count & mark as read ──────────────────────────────────────────

export function getUnreadCount(): number {
  return loadNotificationHistory().filter((n) => !n.read).length;
}

export function markAllAsRead(): void {
  const history = loadNotificationHistory();
  const updated = history.map((n) => ({ ...n, read: true }));
  saveNotificationHistory(updated);
}
