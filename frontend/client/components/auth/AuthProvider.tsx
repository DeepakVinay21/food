import * as React from "react";
import { api, AuthResponse } from "@/lib/api";
import { initPushNotifications } from "@/lib/pushNotifications";
import { scheduleAllNotifications, setupNotificationListeners } from "@/lib/notificationScheduler";

type AuthContextValue = {
  token: string | null;
  email: string | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (payload: { email: string; password: string; confirmPassword: string; firstName: string; lastName: string; age?: number | null }) => Promise<void>;
  verify: (email: string, code: string) => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = React.createContext<AuthContextValue | undefined>(undefined);

const TOKEN_KEY = "foodtrack_token";
const EMAIL_KEY = "foodtrack_email";

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = React.useState<string | null>(() => localStorage.getItem(TOKEN_KEY));
  const [email, setEmail] = React.useState<string | null>(() => localStorage.getItem(EMAIL_KEY));

  const save = (result: AuthResponse) => {
    setToken(result.accessToken);
    setEmail(result.email);
    localStorage.setItem(TOKEN_KEY, result.accessToken);
    localStorage.setItem(EMAIL_KEY, result.email);
  };

  // Register for push notifications + schedule local notifications on mount
  React.useEffect(() => {
    if (token) {
      initPushNotifications(token).catch(() => {});
      setupNotificationListeners();

      // Load prefs and schedule notifications
      const PREF_KEY = "foodtrack_preferences";
      let prefs = { enableAlerts: true, silentHours: false, silentStart: "22:00", silentEnd: "07:00", reminderTiming: "1 Day" as const, notificationTime: "09:00" };
      try {
        const raw = localStorage.getItem(PREF_KEY);
        if (raw) prefs = { ...prefs, ...JSON.parse(raw) };
      } catch {}

      api.products(token).then((res) => {
        scheduleAllNotifications(res.items, prefs);
      }).catch(() => {});
    }
  }, [token]);

  const login = async (userEmail: string, password: string) => {
    const result = await api.login(userEmail, password);
    save(result);
    initPushNotifications(result.accessToken).catch(() => {});
  };

  const register = async (payload: { email: string; password: string; confirmPassword: string; firstName: string; lastName: string; age?: number | null }) => {
    await api.register(payload);
  };

  const verify = async (userEmail: string, code: string) => {
    const result = await api.verify(userEmail, code);
    save(result);
    initPushNotifications(result.accessToken).catch(() => {});
  };

  const logout = async () => {
    if (token) {
      try {
        await api.logout(token);
      } catch {
        // Ignore remote logout failures and clear local session anyway.
      }
    }
    setToken(null);
    setEmail(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EMAIL_KEY);
  };

  return (
    <AuthContext.Provider
      value={{
        token,
        email,
        isAuthenticated: !!token,
        login,
        register,
        verify,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = React.useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return ctx;
}
