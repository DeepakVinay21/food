import { useState, useEffect } from "react";
import {
  Bell,
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
  Play,
  Check,
} from "lucide-react";
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
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { toast } from "sonner";
import { fireInstantNotification, scheduleAllNotifications } from "@/lib/notificationScheduler";
import {
  playNotificationSound,
  NOTIFICATION_SOUNDS,
  getSelectedSound,
  setSelectedSound,
  previewSound,
  type NotificationSoundId,
} from "@/lib/notificationSounds";
import { useTranslation } from "@/lib/i18n/LanguageContext";
import type { Language } from "@/lib/i18n/translations";

const PREF_KEY = "foodtrack_preferences";

type Preferences = {
  enableAlerts: boolean;
  silentHours: boolean;
  silentStart: string;
  silentEnd: string;
  darkMode: boolean;
  language: Language;
  reminderTiming: "7 Days" | "3 Days" | "1 Day" | "On Expiry";
  notificationSound: NotificationSoundId;
};

const REMINDER_TO_DAYS: Record<string, number> = { "7 Days": 7, "3 Days": 3, "1 Day": 1, "On Expiry": 0 };
const DAYS_TO_REMINDER: Record<number, Preferences["reminderTiming"]> = { 7: "7 Days", 3: "3 Days", 1: "1 Day", 0: "On Expiry" };

const defaultPrefs: Preferences = {
  enableAlerts: true,
  silentHours: false,
  silentStart: "22:00",
  silentEnd: "07:00",
  darkMode: false,
  language: "English",
  reminderTiming: "1 Day",
  notificationSound: getSelectedSound(),
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

const SettingItem = ({ icon: Icon, label, value, onClick, destructive, toggle, onToggle, dropdown }: { icon: any; label: string; value?: string; onClick?: () => void; destructive?: boolean; toggle?: boolean; onToggle?: (v: boolean) => void; dropdown?: React.ReactNode }) => (
  <div className={cn("p-4 flex items-center justify-between transition-colors active:bg-muted/50", onClick && "cursor-pointer")} onClick={onClick}>
    <div className="flex items-center gap-3">
      <div className={cn("w-9 h-9 rounded-xl flex items-center justify-center", destructive ? "bg-red-50 dark:bg-red-500/10 text-red-500" : "bg-muted/50 text-muted-foreground")}>
        <Icon className="h-5 w-5" />
      </div>
      <span className={cn("text-sm font-semibold text-foreground", destructive && "text-red-500")}>{label}</span>
    </div>
    <div className="flex items-center gap-2">
      {dropdown || (
        <>
          {value && <span className="text-xs font-medium text-muted-foreground">{value}</span>}
          {toggle !== undefined ? <Switch checked={toggle} onCheckedChange={onToggle} /> : onClick && <ChevronRight className="h-4 w-4 text-muted-foreground/50" />}
        </>
      )}
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

const NotificationSoundDialog = ({
  selectedSound,
  onSelect,
  t,
}: {
  selectedSound: NotificationSoundId;
  onSelect: (id: NotificationSoundId) => void;
  t: (key: any, vars?: any) => string;
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const [current, setCurrent] = useState(selectedSound);

  useEffect(() => {
    if (isOpen) setCurrent(selectedSound);
  }, [isOpen, selectedSound]);

  const soundLabel = NOTIFICATION_SOUNDS.find((s) => s.id === selectedSound)?.name ?? "Fresh Ping";

  return (
    <Dialog open={isOpen} onOpenChange={setIsOpen}>
      <DialogTrigger asChild>
        <div className="w-full">
          <SettingItem icon={Volume2} label={t("profile.notificationSound")} value={soundLabel} />
        </div>
      </DialogTrigger>
      <DialogContent className="max-w-[400px] rounded-3xl p-6">
        <DialogHeader>
          <DialogTitle className="text-xl">{t("profile.notificationSoundDialogTitle")}</DialogTitle>
          <DialogDescription>{t("profile.notificationSoundDialogDesc")}</DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-2 my-3">
          {NOTIFICATION_SOUNDS.map((sound) => (
            <div
              key={sound.id}
              onClick={() => {
                setCurrent(sound.id);
                previewSound(sound.id);
              }}
              className={cn(
                "flex items-center gap-3 p-3 rounded-2xl border cursor-pointer transition-all active:scale-[0.98]",
                current === sound.id
                  ? "border-primary bg-primary/5"
                  : "border-border bg-muted/20 hover:bg-muted/40",
              )}
            >
              <button
                className={cn(
                  "w-9 h-9 rounded-xl flex items-center justify-center shrink-0 transition-colors",
                  current === sound.id
                    ? "bg-primary text-white"
                    : "bg-muted/50 text-muted-foreground",
                )}
                onClick={(e) => {
                  e.stopPropagation();
                  previewSound(sound.id);
                }}
              >
                <Play className="h-4 w-4" />
              </button>
              <div className="flex-1 min-w-0">
                <p className={cn("text-sm font-semibold", current === sound.id ? "text-primary" : "text-foreground")}>{sound.name}</p>
                <p className="text-[11px] text-muted-foreground">{sound.description}</p>
              </div>
              {current === sound.id && (
                <Check className="h-5 w-5 text-primary shrink-0" />
              )}
            </div>
          ))}
        </div>
        <Button
          className="w-full rounded-xl h-11"
          onClick={() => {
            onSelect(current);
            setIsOpen(false);
            toast.success(t("profile.notificationSoundUpdated"));
          }}
        >
          {t("profile.saveSound")}
        </Button>
      </DialogContent>
    </Dialog>
  );
};

const ChangePasswordDialog = ({ token, t }: { token: string; t: (key: any, vars?: any) => string }) => {
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
      setError(t("profile.allFieldsRequired"));
      return;
    }
    if (newPassword !== confirmPassword) {
      setError(t("profile.passwordsMustMatch"));
      return;
    }
    if (newPassword.length < 6) {
      setError(t("profile.passwordMinLength"));
      return;
    }

    setLoading(true);
    try {
      await api.changePassword(token, currentPassword, newPassword);
      toast.success(t("profile.passwordChanged"));
      setIsOpen(false);
    } catch (err: any) {
      setError(err.message || t("profile.failedToChangePassword"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={setIsOpen}>
      <DialogTrigger asChild>
        <div className="w-full">
          <SettingItem icon={Lock} label={t("profile.changePassword")} />
        </div>
      </DialogTrigger>
      <DialogContent className="max-w-[400px] rounded-3xl p-6">
        <DialogHeader>
          <DialogTitle className="text-xl">{t("profile.changePasswordTitle")}</DialogTitle>
          <DialogDescription>
            {t("profile.changePasswordDesc")}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 my-2">
          {error && <div className="p-3 bg-red-100 dark:bg-red-500/10 text-red-600 dark:text-red-400 font-medium text-xs rounded-xl border border-red-200 dark:border-red-500/20">{error}</div>}
          <div className="space-y-2">
            <label className="text-sm font-medium">{t("profile.currentPassword")}</label>
            <input
              type="password"
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">{t("profile.newPassword")}</label>
            <input
              type="password"
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">{t("profile.confirmNewPassword")}</label>
            <input
              type="password"
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
            />
          </div>
        </div>
        <Button className="w-full rounded-xl h-11" onClick={handleSubmit} disabled={loading}>
          {loading ? t("profile.updating") : t("profile.updatePassword")}
        </Button>
      </DialogContent>
    </Dialog>
  );
};

export default function Profile() {
  const { token, logout } = useAuth();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [prefs, setPrefs] = useState<Preferences>(loadPreferences);
  const { t, language, setLanguage } = useTranslation();

  const profileQuery = useQuery({
    queryKey: ["profile"],
    queryFn: () => api.profile(token!),
    enabled: !!token,
  });

  const updatePrefs = (updater: (p: Preferences) => Preferences) => {
    setPrefs((p) => updater(p));
  };

  useEffect(() => {
    localStorage.setItem(PREF_KEY, JSON.stringify(prefs));
    document.documentElement.classList.toggle("dark", prefs.darkMode);

    // Reschedule mobile notifications when alert prefs change
    if (token) {
      api.products(token).then((res) => {
        scheduleAllNotifications(res.items, {
          enableAlerts: prefs.enableAlerts,
          silentHours: prefs.silentHours,
          silentStart: prefs.silentStart,
          silentEnd: prefs.silentEnd,
          reminderTiming: prefs.reminderTiming,
          notificationTime: "09:00",
        });
      }).catch(() => {});
    }
  }, [prefs, token]);

  const languageLabels: Record<Language, string> = {
    English: t("profile.languageEnglish"),
    Hindi: t("profile.languageHindi"),
    Telugu: t("profile.languageTelugu"),
  };

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
        </div>
      </div>

      <ProfileSection title={t("profile.notifications")} description={t("profile.notificationsDescription")}>
        <SettingItem icon={Bell} label={t("profile.enableExpiryAlerts")} toggle={prefs.enableAlerts} onToggle={(v) => updatePrefs((p) => ({ ...p, enableAlerts: v }))} />
        <SettingItem
          icon={Bell}
          label="Test Notification"
          value="Send now"
          onClick={async () => {
            try {
              if (!token) return;
              // Request browser notification permission
              if ("Notification" in window && Notification.permission === "default") {
                await Notification.requestPermission();
              }
              // Send test notifications for all expiring items
              await api.testNotification(token);
              // Trigger immediate refetch so TopNav picks up new notifications + shows toasts
              qc.invalidateQueries({ queryKey: ["notifications"] });
              toast.success("Notifications sent for all expiring items!");
            } catch (err: any) {
              toast.error(err.message || "Failed to send test notification.");
            }
          }}
        />
        <NotificationSoundDialog
          selectedSound={prefs.notificationSound}
          onSelect={(id) => {
            setSelectedSound(id);
            setPrefs((p) => ({ ...p, notificationSound: id }));
          }}
          t={t}
        />
        <SettingItem icon={Moon} label={t("profile.silentHours")} toggle={prefs.silentHours} onToggle={(v) => updatePrefs((p) => ({ ...p, silentHours: v }))} />
        {prefs.silentHours && (
          <div className="p-4 flex flex-col gap-3 animate-in slide-in-from-top-2 duration-200">
            <span className="text-xs font-bold text-muted-foreground/70 px-1 uppercase tracking-wider">{t("profile.quietPeriod")}</span>
            <div className="flex items-center gap-3">
              <div className="flex-1 flex flex-col gap-1">
                <label className="text-[10px] text-muted-foreground font-medium">{t("profile.silentFrom")}</label>
                <input
                  type="time"
                  value={prefs.silentStart}
                  onChange={(e) => updatePrefs((p) => ({ ...p, silentStart: e.target.value }))}
                  className="h-10 rounded-xl border border-input bg-background px-3 text-sm font-medium"
                />
              </div>
              <span className="text-muted-foreground font-bold mt-4">—</span>
              <div className="flex-1 flex flex-col gap-1">
                <label className="text-[10px] text-muted-foreground font-medium">{t("profile.silentTo")}</label>
                <input
                  type="time"
                  value={prefs.silentEnd}
                  onChange={(e) => updatePrefs((p) => ({ ...p, silentEnd: e.target.value }))}
                  className="h-10 rounded-xl border border-input bg-background px-3 text-sm font-medium"
                />
              </div>
            </div>
            <p className="text-[10px] text-muted-foreground">{t("profile.silentHoursNote")}</p>
          </div>
        )}
      </ProfileSection>

      <ProfileSection title={t("profile.appPreferences")}>
        <SettingItem icon={prefs.darkMode ? Moon : Sun} label={t("profile.darkMode")} toggle={prefs.darkMode} onToggle={(v) => setPrefs((p) => ({ ...p, darkMode: v }))} />
        <SettingItem
          icon={Globe}
          label={t("profile.language")}
          value={languageLabels[language]}
          dropdown={
            <select
              value={language}
              onChange={(e) => {
                const lang = e.target.value as Language;
                setPrefs((p) => ({ ...p, language: lang }));
                setLanguage(lang);
                toast.success(t("profile.languageSetTo", { language: languageLabels[lang] }));
              }}
              className="h-10 rounded-xl border border-input bg-background px-3 text-sm font-medium text-foreground appearance-none cursor-pointer pr-8 min-w-[130px]"
              style={{ backgroundImage: `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='%23888' stroke-width='2'%3E%3Cpath d='m6 9 6 6 6-6'/%3E%3C/svg%3E")`, backgroundRepeat: "no-repeat", backgroundPosition: "right 10px center" }}
            >
              {(["English", "Hindi", "Telugu"] as const).map((lang) => (
                <option key={lang} value={lang}>{languageLabels[lang]}</option>
              ))}
            </select>
          }
        />
      </ProfileSection>

      <ProfileSection title={t("profile.accountAndSecurity")}>
        <ChangePasswordDialog token={token!} t={t} />
        <SettingItem icon={Mail} label={t("profile.emailVerification")} value={t("profile.emailVerified")} onClick={() => toast.success(t("profile.emailAlreadyVerified"))} />

        <div className="py-2" />
        <SettingItem
          icon={LogOut}
          label={t("profile.logout")}
          destructive
          onClick={async () => {
            toast(t("profile.loggingOut"));
            await logout();
            navigate("/login");
          }}
        />

        <AlertDialog>
          <AlertDialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={Trash2} label={t("profile.deleteAccount")} destructive />
            </div>
          </AlertDialogTrigger>
          <AlertDialogContent className="rounded-2xl max-w-[400px]">
            <AlertDialogHeader>
              <AlertDialogTitle>{t("profile.deleteAccountTitle")}</AlertDialogTitle>
              <AlertDialogDescription>
                {t("profile.deleteAccountDesc")}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter className="flex-col space-y-2 sm:flex-row sm:justify-end sm:space-x-2 sm:space-y-0">
              <AlertDialogCancel className="rounded-xl mt-0">{t("profile.cancel")}</AlertDialogCancel>
              <AlertDialogAction
                className="rounded-xl bg-destructive text-destructive-foreground hover:bg-destructive/90"
                onClick={async () => {
                  try {
                    await api.deleteAccount(token!);
                    toast.success(t("profile.accountDeleted"));
                    await logout();
                    navigate("/register");
                  } catch (err: any) {
                    toast.error(err.message || t("profile.failedToDeleteAccount"));
                  }
                }}
              >
                {t("profile.deleteAccountConfirm")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </ProfileSection>

      <ProfileSection title={t("profile.helpAndLegal")}>
        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={HelpCircle} label={t("profile.faq")} />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">{t("profile.faqTitle")}</DialogTitle>
              <DialogDescription>{t("profile.faqDesc")}</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-2 max-h-[60vh] overflow-y-auto pr-2">
              <div className="space-y-2">
                <h4 className="font-semibold text-sm">{t("profile.faqHowToAdd")}</h4>
                <p className="text-xs text-muted-foreground leading-relaxed">{t("profile.faqHowToAddAnswer")}</p>
              </div>
              <div className="space-y-2">
                <h4 className="font-semibold text-sm">{t("profile.faqNotifications")}</h4>
                <p className="text-xs text-muted-foreground leading-relaxed">{t("profile.faqNotificationsAnswer")}</p>
              </div>
              <div className="space-y-2">
                <h4 className="font-semibold text-sm">{t("profile.faqDataBackup")}</h4>
                <p className="text-xs text-muted-foreground leading-relaxed">{t("profile.faqDataBackupAnswer")}</p>
              </div>
            </div>
          </DialogContent>
        </Dialog>

        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={MessageSquare} label={t("profile.contactSupport")} />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">{t("profile.contactSupportTitle")}</DialogTitle>
              <DialogDescription>{t("profile.contactSupportDesc")}</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-4">
              <p className="text-sm text-muted-foreground">{t("profile.contactSupportBody")}</p>
              <div className="p-4 bg-muted/50 rounded-2xl flex items-center justify-center border border-border">
                <span className="font-semibold text-primary">{t("profile.contactSupportEmail")}</span>
              </div>
              <p className="text-sm text-muted-foreground text-center">{t("profile.contactSupportResponseTime")}</p>
            </div>
            <Button className="w-full rounded-xl h-11" onClick={() => { navigator.clipboard.writeText("support@foodtrack.example.com"); toast.success(t("profile.emailCopied")); }}>
              {t("profile.copyEmailAddress")}
            </Button>
          </DialogContent>
        </Dialog>

        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={Bug} label={t("profile.reportBug")} />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">{t("profile.reportBugTitle")}</DialogTitle>
              <DialogDescription>{t("profile.reportBugDesc")}</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">{t("profile.reportBugLabel")}</label>
                <textarea
                  className="flex min-h-[120px] w-full rounded-2xl border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
                  placeholder={t("profile.reportBugPlaceholder")}
                />
              </div>
            </div>
            <Button className="w-full rounded-xl h-11" onClick={() => toast.success(t("profile.bugReportSubmitted"))}>
              {t("profile.submitReport")}
            </Button>
          </DialogContent>
        </Dialog>

        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={FileText} label={t("profile.privacyPolicy")} />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">{t("profile.privacyPolicyTitle")}</DialogTitle>
              <DialogDescription>{t("profile.privacyPolicyLastUpdated")}</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-2 max-h-[50vh] overflow-y-auto pr-2 text-sm text-muted-foreground">
              <p>{t("profile.privacyPolicyIntro")}</p>
              <h4 className="font-semibold text-foreground">{t("profile.privacyDataCollection")}</h4>
              <p>{t("profile.privacyDataCollectionBody")}</p>
              <h4 className="font-semibold text-foreground">{t("profile.privacyCameraAccess")}</h4>
              <p>{t("profile.privacyCameraAccessBody")}</p>
            </div>
          </DialogContent>
        </Dialog>

        <Dialog>
          <DialogTrigger asChild>
            <div className="w-full">
              <SettingItem icon={FileText} label={t("profile.termsAndConditions")} />
            </div>
          </DialogTrigger>
          <DialogContent className="max-w-[400px] rounded-3xl p-6">
            <DialogHeader>
              <DialogTitle className="text-xl">{t("profile.termsTitle")}</DialogTitle>
              <DialogDescription>{t("profile.termsDesc")}</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 my-2 max-h-[50vh] overflow-y-auto pr-2 text-sm text-muted-foreground">
              <p>{t("profile.termsIntro")}</p>
              <h4 className="font-semibold text-foreground">{t("profile.termsUsageRestrictions")}</h4>
              <p>{t("profile.termsUsageRestrictionsBody")}</p>
              <h4 className="font-semibold text-foreground">{t("profile.termsLiability")}</h4>
              <p>{t("profile.termsLiabilityBody")}</p>
            </div>
          </DialogContent>
        </Dialog>

        <div className="p-4 flex items-center justify-between text-muted-foreground/50 border-t border-border/50">
          <span className="text-xs font-bold">{t("profile.appVersion")}</span>
          <span className="text-xs font-medium">{t("profile.appVersionValue")}</span>
        </div>
      </ProfileSection>
    </div>
  );
}
