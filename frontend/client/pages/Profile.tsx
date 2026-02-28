import { useState, useEffect } from "react";
import {
  Bell,
  BellRing,
  ChevronRight,
  Globe,
  Lock,
  LogOut,
  Mail,
  MessageSquare,
  Moon,
  ShieldCheck,
  Trash2,
  User,
  HelpCircle,
  Bug,
  FileText,
  Clock,
  Volume2,
  Sun,
} from "lucide-react";
import { Capacitor } from "@capacitor/core";
import { LocalNotifications } from "@capacitor/local-notifications";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { useAuth } from "@/components/auth/AuthProvider";
import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { toast } from "sonner";

const PREF_KEY = "foodtrack_preferences";

type Preferences = {
  enableAlerts: boolean;
  silentHours: boolean;
  silentStart: string;
  silentEnd: string;
  darkMode: boolean;
  language: "English" | "Hindi" | "Telugu";
  reminderTiming: "7 Days" | "3 Days" | "1 Day" | "On Expiry";
};

const defaultPrefs: Preferences = {
  enableAlerts: true,
  silentHours: false,
  silentStart: "22:00",
  silentEnd: "07:00",
  darkMode: false,
  language: "English",
  reminderTiming: "1 Day",
};

const ProfileSection = ({ title, children, description }: { title: string; children: React.ReactNode; description?: string }) => (
  <div className="flex flex-col gap-3 px-6 py-4">
    <div className="flex flex-col gap-0.5">
      <h3 className="text-lg font-bold text-foreground">{title}</h3>
      {description && <p className="text-xs text-muted-foreground">{description}</p>}
    </div>
    <div className="bg-card rounded-[2rem] border border-border overflow-hidden shadow-sm flex flex-col divide-y divide-border/50">{children}</div>
  </div>
);

const SettingItem = ({ icon: Icon, label, value, onClick, destructive, toggle, onToggle }: { icon: any; label: string; value?: string; onClick?: () => void; destructive?: boolean; toggle?: boolean; onToggle?: (v: boolean) => void }) => (
  <div className={cn("p-4 flex items-center justify-between transition-colors active:bg-muted/50", onClick && "cursor-pointer")} onClick={onClick}>
    <div className="flex items-center gap-3">
      <div className={cn("w-9 h-9 rounded-xl flex items-center justify-center", destructive ? "bg-red-50 dark:bg-red-500/10 text-red-500" : "bg-muted/50 text-muted-foreground")}>
        <Icon className="h-5 w-5" />
      </div>
      <span className={cn("text-sm font-semibold text-foreground", destructive && "text-red-500")}>{label}</span>
    </div>
    <div className="flex items-center gap-2">
      {value && <span className="text-xs font-medium text-muted-foreground">{value}</span>}
      {toggle !== undefined ? <Switch checked={toggle} onCheckedChange={onToggle} /> : onClick && <ChevronRight className="h-4 w-4 text-muted-foreground/50" />}
    </div>
  </div>
);

function loadPreferences(): Preferences {
  const raw = localStorage.getItem(PREF_KEY);
  if (!raw) return defaultPrefs;
  try {
    return { ...defaultPrefs, ...(JSON.parse(raw) as Partial<Preferences>) };
  } catch {
    return defaultPrefs;
  }
}

const ChangePasswordDialog = ({ token }: { token: string }) => {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [isOpen, setIsOpen] = useState(false);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setError("");
    }
  }, [isOpen]);

  const handleSubmit = async () => {
    setError("");
    if (!currentPassword || !newPassword || !confirmPassword) {
      setError("All fields are required.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("New password and confirm password must match.");
      return;
    }
    if (newPassword.length < 6) {
      setError("Password must be at least 6 characters.");
      return;
    }

    setLoading(true);
    try {
      await api.changePassword(token, currentPassword, newPassword);
      toast.success("Password changed successfully!");
      setIsOpen(false);
    } catch (err: any) {
      setError(err.message || "Failed to change password.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={setIsOpen}>
      <DialogTrigger asChild>
        <div className="w-full">
          <SettingItem icon={Lock} label="Change password" />
        </div>
      </DialogTrigger>
      <DialogContent className="max-w-[400px] rounded-3xl p-6">
        <DialogHeader>
          <DialogTitle className="text-xl">Change Password</DialogTitle>
          <DialogDescription>
            Update your account password securely.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 my-2">
          {error && <div className="p-3 bg-red-100 dark:bg-red-500/10 text-red-600 dark:text-red-400 font-medium text-xs rounded-xl border border-red-200 dark:border-red-500/20">{error}</div>}
          <div className="space-y-2">
            <label className="text-sm font-medium">Current Password</label>
            <input
              type="password"
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">New Password</label>
            <input
              type="password"
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">Confirm New Password</label>
            <input
              type="password"
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
            />
          </div>
        </div>
        <Button className="w-full rounded-xl h-11" onClick={handleSubmit} disabled={loading}>
          {loading ? "Updating..." : "Update Password"}
        </Button>
      </DialogContent>
    </Dialog>
  );
};

const TestNotificationButton = ({ token }: { token: string }) => {
  const [testing, setTesting] = useState(false);
  const [countdown, setCountdown] = useState(0);

  const sendTestNotification = async () => {
    setTesting(true);
    setCountdown(5);

    try {
      if (Capacitor.isNativePlatform()) {
        // Request permission
        const perm = await LocalNotifications.requestPermissions();
        if (perm.display !== "granted") {
          toast.error("Notification permission denied. Enable it in app settings.");
          setTesting(false);
          return;
        }

        // Schedule local notification in 5 seconds
        await LocalNotifications.schedule({
          notifications: [
            {
              id: Date.now(),
              title: "Food Expiry Reminder",
              body: "Test: Your Milk expires tomorrow! Check your pantry.",
              schedule: { at: new Date(Date.now() + 5000) },
              sound: "default",
              smallIcon: "ic_launcher",
              largeIcon: "ic_launcher",
            },
          ],
        });

        // Countdown timer
        let c = 5;
        const interval = setInterval(() => {
          c--;
          setCountdown(c);
          if (c <= 0) {
            clearInterval(interval);
            setTesting(false);
            toast.success("Notification sent! Check your notification tray.");
          }
        }, 1000);
      } else {
        // Web fallback: use browser Notification API
        if ("Notification" in window) {
          const perm = await Notification.requestPermission();
          if (perm === "granted") {
            setTimeout(() => {
              new Notification("Food Expiry Reminder", {
                body: "Test: Your Milk expires tomorrow! Check your pantry.",
                icon: "/favicon.ico",
              });
              setTesting(false);
              toast.success("Notification sent!");
            }, 5000);

            let c = 5;
            const interval = setInterval(() => {
              c--;
              setCountdown(c);
              if (c <= 0) clearInterval(interval);
            }, 1000);
          } else {
            toast.error("Notification permission denied.");
            setTesting(false);
          }
        } else {
          toast.error("Notifications not supported.");
          setTesting(false);
        }
      }

      // Also trigger backend daily job to test server-side notifications
      try {
        await api.runNotifications(token);
      } catch {
        // Backend notification is optional for this test
      }
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to send test notification.");
      setTesting(false);
    }
  };

  return (
    <div
      className="p-4 flex items-center justify-between transition-colors active:bg-muted/50 cursor-pointer"
      onClick={!testing ? sendTestNotification : undefined}
    >
      <div className="flex items-center gap-3">
        <div className="w-9 h-9 rounded-xl flex items-center justify-center bg-green-50 dark:bg-green-500/10 text-green-600">
          <BellRing className="h-5 w-5" />
        </div>
        <div className="flex flex-col">
          <span className="text-sm font-semibold text-foreground">Test Notification</span>
          <span className="text-[10px] text-muted-foreground">Sends a test alert in 5 seconds</span>
        </div>
      </div>
      <div className="flex items-center gap-2">
        {testing ? (
          <span className="text-xs font-bold text-primary animate-pulse">{countdown}s</span>
        ) : (
          <ChevronRight className="h-4 w-4 text-muted-foreground/50" />
        )}
      </div>
    </div>
  );
};

export default function Profile() {
  const { token, logout } = useAuth();
  const navigate = useNavigate();
  const [prefs, setPrefs] = useState<Preferences>(loadPreferences);

  const profileQuery = useQuery({
    queryKey: ["profile"],
    queryFn: () => api.profile(token!),
    enabled: !!token,
  });

  useEffect(() => {
    localStorage.setItem(PREF_KEY, JSON.stringify(prefs));
    document.documentElement.classList.toggle("dark", prefs.darkMode);
  }, [prefs]);

  return (
    <div className="flex flex-col gap-2 pb-24 animate-in slide-in-from-right duration-300">
      <div className="px-6 pt-8 pb-6 flex flex-col items-center gap-4">
        <div className="relative group cursor-pointer" onClick={() => navigate("/profile/edit")}>
          {profileQuery.data?.profilePhotoDataUrl ? (
            <img src={profileQuery.data.profilePhotoDataUrl} alt="profile" className="w-24 h-24 rounded-[2.5rem] border-4 border-card shadow-xl object-cover" />
          ) : (
            <div className="w-24 h-24 rounded-[2.5rem] bg-primary/10 border-4 border-card shadow-xl flex items-center justify-center text-primary overflow-hidden">
              <span className="text-3xl font-bold uppercase tracking-widest">{(profileQuery.data?.firstName?.[0] ?? "U") + (profileQuery.data?.lastName?.[0] ?? "")}</span>
            </div>
          )}
          <button className="absolute bottom-0 right-0 w-8 h-8 bg-primary text-white rounded-full border-2 border-card flex items-center justify-center shadow-lg active:scale-95 transition-transform">
            <User className="h-4 w-4" />
          </button>
        </div>
        <div className="text-center">
          <h2 className="text-xl font-bold text-foreground leading-tight">{profileQuery.data?.firstName} {profileQuery.data?.lastName}</h2>
          <p className="text-sm text-muted-foreground">{profileQuery.data?.email}</p>
          <p className="text-xs text-muted-foreground">Age: {profileQuery.data?.age ?? "-"}</p>
        </div>
      </div>

      <ProfileSection title="Notifications" description="Control how and when you receive alerts.">
        <SettingItem icon={Bell} label="Enable Expiry Alerts" toggle={prefs.enableAlerts} onToggle={(v) => setPrefs((p) => ({ ...p, enableAlerts: v }))} />
        <div className="p-4 flex flex-col gap-3">
          <span className="text-xs font-bold text-muted-foreground/70 px-1 uppercase tracking-wider">Reminder Timing</span>
          <div className="flex flex-wrap gap-2">
            {(["7 Days", "3 Days", "1 Day", "On Expiry"] as const).map((d) => (
              <Badge
                key={d}
                variant="outline"
                onClick={() => setPrefs((p) => ({ ...p, reminderTiming: d }))}
                className={cn("rounded-xl px-3 h-8 border-border bg-muted/20 text-muted-foreground transition-all cursor-pointer active:scale-95", prefs.reminderTiming === d ? "bg-primary/10 border-primary text-primary" : "")}
              >
                {d}
              </Badge>
            ))}
          </div>
        </div>
        <SettingItem icon={Volume2} label="Notification Sound" value="Default" onClick={() => toast("Notification sound settings saved.")} />
        <SettingItem icon={Moon} label="Silent Hours" toggle={prefs.silentHours} onToggle={(v) => setPrefs((p) => ({ ...p, silentHours: v }))} />
        {prefs.silentHours && (
          <div className="p-4 flex flex-col gap-3 animate-in slide-in-from-top-2 duration-200">
            <span className="text-xs font-bold text-muted-foreground/70 px-1 uppercase tracking-wider">Quiet Period</span>
            <div className="flex items-center gap-3">
              <div className="flex-1 flex flex-col gap-1">
                <label className="text-[10px] text-muted-foreground font-medium">From</label>
                <input
                  type="time"
                  value={prefs.silentStart}
                  onChange={(e) => setPrefs((p) => ({ ...p, silentStart: e.target.value }))}
                  className="h-10 rounded-xl border border-input bg-background px-3 text-sm font-medium"
                />
              </div>
              <span className="text-muted-foreground font-bold mt-4">—</span>
              <div className="flex-1 flex flex-col gap-1">
                <label className="text-[10px] text-muted-foreground font-medium">To</label>
                <input
                  type="time"
                  value={prefs.silentEnd}
                  onChange={(e) => setPrefs((p) => ({ ...p, silentEnd: e.target.value }))}
                  className="h-10 rounded-xl border border-input bg-background px-3 text-sm font-medium"
                />
              </div>
            </div>
            <p className="text-[10px] text-muted-foreground">No alerts will be sent during this period.</p>
          </div>
        )}
        <TestNotificationButton token={token!} />
      </ProfileSection>

      <ProfileSection title="App Preferences">
        <SettingItem icon={prefs.darkMode ? Moon : Sun} label="Dark Mode" toggle={prefs.darkMode} onToggle={(v) => setPrefs((p) => ({ ...p, darkMode: v }))} />
        <div className="p-4 flex flex-col gap-3">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl flex items-center justify-center bg-muted/50 text-muted-foreground">
              <Globe className="h-5 w-5" />
            </div>
            <span className="text-sm font-semibold text-foreground">Language</span>
          </div>
          <div className="flex flex-wrap gap-2 mt-1">
            {(["English", "Hindi", "Telugu"] as const).map((lang) => (
              <Badge
                key={lang}
                variant="outline"
                onClick={() => { setPrefs((p) => ({ ...p, language: lang })); toast.success(`Language set to ${lang}`); }}
                className={cn("rounded-xl px-4 h-9 border-border bg-muted/20 text-muted-foreground transition-all cursor-pointer active:scale-95 text-sm font-medium", prefs.language === lang ? "bg-primary/10 border-primary text-primary" : "")}
              >
                {lang}
              </Badge>
            ))}
          </div>
        </div>
      </ProfileSection>

      <ProfileSection title="Account & Security">
        <ChangePasswordDialog token={token!} />
        <SettingItem icon={Mail} label="Email Verification" value="Verified" onClick={() => toast.success("Your email is already verified.")} />

        <div className="py-2" />
        <SettingItem
          icon={LogOut}
          label="Logout"
          destructive
          onClick={async () => {
            toast("Logging out...");
            await logout();
            navigate("/login");
          }}
        />

        <AlertDialog>
          <AlertDialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={Trash2} label="Delete Account" destructive />
            </div>
          </AlertDialogTrigger>
          <AlertDialogContent className="rounded-2xl max-w-[400px]">
            <AlertDialogHeader>
              <AlertDialogTitle>Are you absolutely sure?</AlertDialogTitle>
              <AlertDialogDescription>
                This action cannot be undone. This will permanently delete your
                account, remove your credentials, and wipe all your data from our servers.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter className="flex-col space-y-2 sm:flex-row sm:justify-end sm:space-x-2 sm:space-y-0">
              <AlertDialogCancel className="rounded-xl mt-0">Cancel</AlertDialogCancel>
              <AlertDialogAction
                className="rounded-xl bg-destructive text-destructive-foreground hover:bg-destructive/90"
                onClick={async () => {
                  try {
                    await api.deleteAccount(token!);
                    toast.success("Account deleted successfully.");
                    await logout();
                    navigate("/register");
                  } catch (err: any) {
                    toast.error(err.message || "Failed to delete account.");
                  }
                }}
              >
                Delete Account
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </ProfileSection>

      <ProfileSection title="Help & Legal">
        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={HelpCircle} label="FAQ" />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">Frequently Asked Questions</DialogTitle>
              <DialogDescription>Common questions about Food Expiration Tracker.</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-2 max-h-[60vh] overflow-y-auto pr-2">
              <div className="space-y-2">
                <h4 className="font-semibold text-sm">How do I add items?</h4>
                <p className="text-xs text-muted-foreground leading-relaxed">You can add items via the Scan page to automatically detect items using your camera, or manually from the pantry.</p>
              </div>
              <div className="space-y-2">
                <h4 className="font-semibold text-sm">How do notifications work?</h4>
                <p className="text-xs text-muted-foreground leading-relaxed">The app will send you notifications based on your chosen Reminder Timing preferences in the Profile section.</p>
              </div>
              <div className="space-y-2">
                <h4 className="font-semibold text-sm">Is my data backed up?</h4>
                <p className="text-xs text-muted-foreground leading-relaxed">Your data is stored securely on our servers and synced across your devices.</p>
              </div>
            </div>
          </DialogContent>
        </Dialog>

        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={MessageSquare} label="Contact Support" />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">Contact Support</DialogTitle>
              <DialogDescription>We're here to help you out!</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-4">
              <p className="text-sm text-muted-foreground">You can reach our dedicated support team via email at:</p>
              <div className="p-4 bg-muted/50 rounded-2xl flex items-center justify-center border border-border">
                <span className="font-semibold text-primary">support@foodtrack.example.com</span>
              </div>
              <p className="text-sm text-muted-foreground text-center">Typical response time: 24-48 hours.</p>
            </div>
            <Button className="w-full rounded-xl h-11" onClick={() => { navigator.clipboard.writeText("support@foodtrack.example.com"); toast.success("Copied email address to clipboard!"); }}>
              Copy Email Address
            </Button>
          </DialogContent>
        </Dialog>

        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={Bug} label="Report a Bug" />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">Report a Bug</DialogTitle>
              <DialogDescription>Found something that isn't working?</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">Describe the issue</label>
                <textarea
                  className="flex min-h-[120px] w-full rounded-2xl border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
                  placeholder="What happened? What were you trying to do?"
                />
              </div>
            </div>
            <Button className="w-full rounded-xl h-11" onClick={() => toast.success("Bug report submitted. Thank you!")}>
              Submit Report
            </Button>
          </DialogContent>
        </Dialog>

        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={FileText} label="Privacy Policy" />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">Privacy Policy</DialogTitle>
              <DialogDescription>Last updated: February 2026</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-2 max-h-[50vh] overflow-y-auto pr-2 text-sm text-muted-foreground">
              <p>We respect your privacy and are committed to protecting it through our compliance with this policy.</p>
              <h4 className="font-semibold text-foreground">Data Collection</h4>
              <p>The Food Expiration Tracker stores your pantry data securely on our servers. We do not sell your personal data to third parties.</p>
              <h4 className="font-semibold text-foreground">Camera Access</h4>
              <p>Camera access is used locally on your device to analyze and extract product names and expiration dates. Images are processed securely.</p>
            </div>
          </DialogContent>
        </Dialog>

        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={FileText} label="Terms & Conditions" />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">Terms & Conditions</DialogTitle>
              <DialogDescription>Please read these terms carefully.</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-2 max-h-[50vh] overflow-y-auto pr-2 text-sm text-muted-foreground">
              <p>By downloading or using the app, these terms will automatically apply to you.</p>
              <h4 className="font-semibold text-foreground">Usage Restrictions</h4>
              <p>You may not copy, modify the app, any part of the app, or our trademarks in any way.</p>
              <h4 className="font-semibold text-foreground">Liability</h4>
              <p>The recipes and suggestions provided by the app are for informational purposes only. Proceed with caution and common sense.</p>
            </div>
          </DialogContent>
        </Dialog>

        <div className="p-4 flex items-center justify-between text-muted-foreground/50 border-t border-border/50">
          <span className="text-xs font-bold">App Version</span>
          <span className="text-xs font-medium">1.0.4 (Build 42)</span>
        </div>
      </ProfileSection>
    </div>
  );
}
