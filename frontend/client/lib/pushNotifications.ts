import { Capacitor } from "@capacitor/core";
import { PushNotifications } from "@capacitor/push-notifications";
import { api } from "./api";

let initialized = false;

export async function initPushNotifications(authToken: string) {
  if (!Capacitor.isNativePlatform() || initialized) {
    return;
  }

  const permResult = await PushNotifications.requestPermissions();
  if (permResult.receive !== "granted") {
    console.warn("Push notification permission not granted");
    return;
  }

  await PushNotifications.register();

  PushNotifications.addListener("registration", async (token) => {
    console.log("FCM token:", token.value);
    try {
      await api.registerDevice(authToken, token.value);
      console.log("Device registered with backend");
    } catch (err) {
      console.error("Failed to register device token:", err);
    }
  });

  PushNotifications.addListener("registrationError", (error) => {
    console.error("Push registration error:", error);
  });

  PushNotifications.addListener("pushNotificationReceived", (notification) => {
    console.log("Push received (foreground):", notification);
  });

  PushNotifications.addListener("pushNotificationActionPerformed", (action) => {
    console.log("Push action performed:", action);
  });

  initialized = true;
}
