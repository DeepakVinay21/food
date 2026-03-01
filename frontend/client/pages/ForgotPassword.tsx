import { useState, useEffect } from "react";
import { useNavigate, Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { InputOTP, InputOTPGroup, InputOTPSlot } from "@/components/ui/input-otp";
import { api } from "@/lib/api";
import { Mail, ArrowLeft, KeyRound, ShieldCheck } from "lucide-react";
import { useTranslation } from "@/lib/i18n/LanguageContext";

type Step = "email" | "otp";

export default function ForgotPassword() {
  const [step, setStep] = useState<Step>("email");
  const [email, setEmail] = useState("");
  const [otpCode, setOtpCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [countdown, setCountdown] = useState(0);
  const navigate = useNavigate();
  const { t } = useTranslation();

  useEffect(() => {
    if (countdown <= 0) return;
    const timer = setTimeout(() => setCountdown((c) => c - 1), 1000);
    return () => clearTimeout(timer);
  }, [countdown]);

  const handleSendCode = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await api.forgotPassword(email);
      setStep("otp");
      setCountdown(60);
    } catch (err) {
      setError(err instanceof Error ? err.message : t("forgotPassword.failedToSendCode"));
    } finally {
      setLoading(false);
    }
  };

  const handleResetPassword = async () => {
    if (otpCode.length !== 6) return;
    if (!newPassword || !confirmPassword) {
      setError(t("forgotPassword.enterNewPassword"));
      return;
    }
    if (newPassword !== confirmPassword) {
      setError(t("forgotPassword.passwordsDoNotMatch"));
      return;
    }
    if (newPassword.length < 6) {
      setError(t("forgotPassword.passwordMinLength"));
      return;
    }
    setError("");
    setLoading(true);
    try {
      await api.resetPassword(email, otpCode, newPassword, confirmPassword);
      navigate("/login");
    } catch (err) {
      setError(err instanceof Error ? err.message : t("forgotPassword.resetFailed"));
      setOtpCode("");
    } finally {
      setLoading(false);
    }
  };

  const handleResend = async () => {
    setError("");
    setLoading(true);
    try {
      await api.forgotPassword(email);
      setCountdown(60);
      setOtpCode("");
    } catch (err) {
      setError(err instanceof Error ? err.message : t("forgotPassword.failedToResendCode"));
    } finally {
      setLoading(false);
    }
  };

  if (step === "otp") {
    return (
      <div className="h-full flex items-center justify-center p-6">
        <div className="w-full max-w-sm bg-card rounded-[2rem] border border-border p-6 flex flex-col gap-5 shadow-lg items-center animate-in fade-in zoom-in-95 duration-300">
          <div className="w-16 h-16 rounded-full bg-primary/10 flex items-center justify-center">
            <KeyRound className="h-8 w-8 text-primary" />
          </div>

          <div className="text-center">
            <h2 className="text-2xl font-bold text-foreground">{t("forgotPassword.resetPassword")}</h2>
            <p className="text-sm text-muted-foreground mt-1">
              {t("forgotPassword.weSentCode")}
            </p>
            <p className="text-sm font-semibold text-foreground">{email}</p>
          </div>

          <InputOTP maxLength={6} value={otpCode} onChange={setOtpCode}>
            <InputOTPGroup className="gap-2">
              <InputOTPSlot index={0} className="w-12 h-14 text-lg font-bold rounded-xl border-border" />
              <InputOTPSlot index={1} className="w-12 h-14 text-lg font-bold rounded-xl border-border" />
              <InputOTPSlot index={2} className="w-12 h-14 text-lg font-bold rounded-xl border-border" />
              <InputOTPSlot index={3} className="w-12 h-14 text-lg font-bold rounded-xl border-border" />
              <InputOTPSlot index={4} className="w-12 h-14 text-lg font-bold rounded-xl border-border" />
              <InputOTPSlot index={5} className="w-12 h-14 text-lg font-bold rounded-xl border-border" />
            </InputOTPGroup>
          </InputOTP>

          <div className="w-full flex flex-col gap-3">
            <Input
              placeholder={t("forgotPassword.newPasswordPlaceholder")}
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              className="rounded-xl h-11"
            />
            <Input
              placeholder={t("forgotPassword.confirmNewPasswordPlaceholder")}
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              className="rounded-xl h-11"
            />
          </div>

          {error && <p className="text-xs text-red-500 font-medium text-center">{error}</p>}

          <Button
            className="rounded-xl h-11 font-bold w-full"
            disabled={loading || otpCode.length !== 6}
            onClick={handleResetPassword}
          >
            <ShieldCheck className="h-4 w-4 mr-2" />
            {loading ? t("forgotPassword.resetting") : t("forgotPassword.resetPasswordButton")}
          </Button>

          <div className="flex flex-col items-center gap-2">
            <button
              type="button"
              className="text-sm text-primary font-semibold disabled:text-muted-foreground disabled:cursor-not-allowed"
              disabled={countdown > 0 || loading}
              onClick={handleResend}
            >
              {countdown > 0 ? t("forgotPassword.resendCodeIn", { seconds: countdown }) : t("forgotPassword.resendCode")}
            </button>
            <button
              type="button"
              className="text-sm text-muted-foreground flex items-center gap-1"
              onClick={() => { setStep("email"); setOtpCode(""); setNewPassword(""); setConfirmPassword(""); setError(""); }}
            >
              <ArrowLeft className="h-3 w-3" /> {t("forgotPassword.back")}
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full flex items-center justify-center p-6">
      <form onSubmit={handleSendCode} className="w-full max-w-sm bg-card rounded-[2rem] border border-border p-6 flex flex-col gap-4 shadow-lg">
        <div className="flex items-center justify-center">
          <div className="w-16 h-16 rounded-full bg-primary/10 flex items-center justify-center">
            <Mail className="h-8 w-8 text-primary" />
          </div>
        </div>
        <h2 className="text-2xl font-bold text-foreground text-center">{t("forgotPassword.title")}</h2>
        <p className="text-sm text-muted-foreground text-center -mt-2">{t("forgotPassword.subtitle")}</p>
        <Input
          placeholder={t("forgotPassword.emailPlaceholder")}
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          className="rounded-xl h-11"
        />
        {error && <p className="text-xs text-red-500 font-medium">{error}</p>}
        <Button type="submit" className="rounded-xl h-11 font-bold" disabled={loading}>
          {loading ? t("forgotPassword.sendingCode") : t("forgotPassword.sendResetCode")}
        </Button>
        <p className="text-sm text-muted-foreground text-center">
          <Link className="text-primary font-semibold flex items-center justify-center gap-1" to="/login">
            <ArrowLeft className="h-3 w-3" /> {t("forgotPassword.backToLogin")}
          </Link>
        </p>
      </form>
    </div>
  );
}
