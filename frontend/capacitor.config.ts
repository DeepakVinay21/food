import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "com.foodtracker.app",
  appName: "FoodTracker",
  webDir: "dist/spa",
  android: {
    allowMixedContent: true,
  },
  server: {
    cleartext: true,
    androidScheme: "http",
  },
  plugins: {
    PushNotifications: {
      presentationOptions: ["badge", "sound", "alert"],
    },
  },
};

export default config;
