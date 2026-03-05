import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/components/auth/AuthProvider";
import { useTranslation } from "@/lib/i18n/LanguageContext";

export default function Register() {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const auth = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation();

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");

    if (password !== confirmPassword) {
      setError(t("register.passwordsMustMatch"));
      return;
    }

    setLoading(true);
    try {
      await auth.register({
        email,
        password,
        confirmPassword,
        firstName,
        lastName,
      });
      navigate("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : t("register.registrationFailed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="h-full flex items-center justify-center p-6">
      <form onSubmit={handleRegister} className="w-full max-w-sm bg-card rounded-[2rem] border border-border p-6 flex flex-col gap-3 shadow-lg">
        <h2 className="text-2xl font-bold text-foreground">{t("register.createAccount")}</h2>
        <p className="text-sm text-muted-foreground -mt-2">{t("register.signUpSubtitle")}</p>
        <Input placeholder={t("register.firstNamePlaceholder")} value={firstName} onChange={(e) => setFirstName(e.target.value)} required className="rounded-xl h-11" />
        <Input placeholder={t("register.lastNamePlaceholder")} value={lastName} onChange={(e) => setLastName(e.target.value)} required className="rounded-xl h-11" />
        <Input placeholder={t("register.emailPlaceholder")} type="email" value={email} onChange={(e) => setEmail(e.target.value)} required className="rounded-xl h-11" />
        <Input placeholder={t("register.passwordPlaceholder")} type="password" value={password} onChange={(e) => setPassword(e.target.value)} required className="rounded-xl h-11" />
        <Input placeholder={t("register.confirmPasswordPlaceholder")} type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} required className="rounded-xl h-11" />
        {error && <p className="text-xs text-red-500 font-medium">{error}</p>}
        <Button type="submit" className="rounded-xl h-11 font-bold mt-1" disabled={loading}>
          {loading ? t("register.creatingAccount") : t("register.createAccountButton")}
        </Button>
        <p className="text-sm text-muted-foreground text-center">{t("register.alreadyHaveAccount")} <Link className="text-primary font-semibold" to="/login">{t("register.login")}</Link></p>
      </form>
    </div>
  );
}
