import { useState, useEffect, useRef } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/components/auth/AuthProvider";
import { api } from "@/lib/api";
import { toast } from "sonner";

const CODE_LENGTH = 6;
const EXPIRY_SECONDS = 5 * 60;
const RESEND_COOLDOWN = 60;

export default function VerifyEmail() {
  const [searchParams] = useSearchParams();
  const email = searchParams.get("email") ?? "";
  const navigate = useNavigate();
  const auth = useAuth();

  const [digits, setDigits] = useState<string[]>(Array(CODE_LENGTH).fill(""));
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [secondsLeft, setSecondsLeft] = useState(EXPIRY_SECONDS);
  const [resendCooldown, setResendCooldown] = useState(RESEND_COOLDOWN);
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

  // Expiry countdown
  useEffect(() => {
    if (secondsLeft <= 0) return;
    const timer = setInterval(() => setSecondsLeft((s) => s - 1), 1000);
    return () => clearInterval(timer);
  }, [secondsLeft]);

  // Resend cooldown
  useEffect(() => {
    if (resendCooldown <= 0) return;
    const timer = setInterval(() => setResendCooldown((s) => s - 1), 1000);
    return () => clearInterval(timer);
  }, [resendCooldown]);

  const formatTime = (s: number) => {
    const min = Math.floor(Math.max(0, s) / 60);
    const sec = Math.max(0, s) % 60;
    return `${min}:${sec.toString().padStart(2, "0")}`;
  };

  const handleChange = (index: number, value: string) => {
    if (!/^\d*$/.test(value)) return;
    const newDigits = [...digits];
    newDigits[index] = value.slice(-1);
    setDigits(newDigits);
    setError("");

    if (value && index < CODE_LENGTH - 1) {
      inputRefs.current[index + 1]?.focus();
    }
  };

  const handleKeyDown = (index: number, e: React.KeyboardEvent) => {
    if (e.key === "Backspace" && !digits[index] && index > 0) {
      inputRefs.current[index - 1]?.focus();
    }
  };

  const handlePaste = (e: React.ClipboardEvent) => {
    e.preventDefault();
    const pasted = e.clipboardData.getData("text").replace(/\D/g, "").slice(0, CODE_LENGTH);
    if (!pasted) return;
    const newDigits = [...digits];
    for (let i = 0; i < pasted.length; i++) {
      newDigits[i] = pasted[i];
    }
    setDigits(newDigits);
    const focusIndex = Math.min(pasted.length, CODE_LENGTH - 1);
    inputRefs.current[focusIndex]?.focus();
  };

  const handleVerify = async () => {
    const code = digits.join("");
    if (code.length !== CODE_LENGTH) {
      setError("Please enter the full 6-digit code.");
      return;
    }

    setLoading(true);
    setError("");
    try {
      await auth.verify(email, code);
      toast.success("Email verified! Welcome to FoodTracker.");
      navigate("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Verification failed.");
    } finally {
      setLoading(false);
    }
  };

  const handleResend = async () => {
    if (resendCooldown > 0) return;
    try {
      await api.resend(email);
      toast.success("New verification code sent!");
      setResendCooldown(RESEND_COOLDOWN);
      setSecondsLeft(EXPIRY_SECONDS);
      setDigits(Array(CODE_LENGTH).fill(""));
      setError("");
      inputRefs.current[0]?.focus();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not resend code.");
    }
  };

  if (!email) {
    navigate("/register");
    return null;
  }

  return (
    <div className="h-full flex items-center justify-center p-6">
      <div className="w-full max-w-sm bg-card rounded-[2rem] border border-border p-6 flex flex-col gap-4 shadow-lg">
        <h2 className="text-2xl font-bold text-foreground">Verify Your Email</h2>
        <p className="text-sm text-muted-foreground -mt-2">
          We sent a 6-digit code to <span className="font-medium text-foreground">{email}</span>
        </p>

        <div className="flex gap-2 justify-center" onPaste={handlePaste}>
          {digits.map((digit, i) => (
            <Input
              key={i}
              ref={(el) => { inputRefs.current[i] = el; }}
              type="text"
              inputMode="numeric"
              maxLength={1}
              value={digit}
              onChange={(e) => handleChange(i, e.target.value)}
              onKeyDown={(e) => handleKeyDown(i, e)}
              className="w-11 h-12 text-center text-xl font-bold rounded-xl"
              autoFocus={i === 0}
            />
          ))}
        </div>

        <p className="text-xs text-muted-foreground text-center">
          {secondsLeft > 0
            ? `Code expires in ${formatTime(secondsLeft)}`
            : "Code expired. Please request a new one."}
        </p>

        {error && <p className="text-xs text-red-500 font-medium text-center">{error}</p>}

        <Button
          onClick={handleVerify}
          className="rounded-xl h-11 font-bold"
          disabled={loading || secondsLeft <= 0}
        >
          {loading ? "Verifying..." : "Verify"}
        </Button>

        <button
          type="button"
          onClick={handleResend}
          disabled={resendCooldown > 0}
          className="text-sm text-center text-primary font-semibold disabled:text-muted-foreground disabled:cursor-not-allowed"
        >
          {resendCooldown > 0 ? `Resend code in ${resendCooldown}s` : "Resend code"}
        </button>
      </div>
    </div>
  );
}
