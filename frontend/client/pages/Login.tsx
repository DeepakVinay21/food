import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/components/auth/AuthProvider";
import { useTranslation } from "@/lib/i18n/LanguageContext";

export default function Login() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const auth = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation();

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await auth.login(email, password);
      navigate("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : t("login.failed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="h-full flex items-center justify-center p-6">
      <form onSubmit={onSubmit} className="w-full max-w-sm bg-card rounded-[2rem] border border-border p-6 flex flex-col gap-4 shadow-lg">
        <h2 className="text-2xl font-bold text-foreground">{t("login.welcomeBack")}</h2>
        <p className="text-sm text-muted-foreground -mt-2">{t("login.signInSubtitle")}</p>
        <Input placeholder={t("login.emailPlaceholder")} type="email" value={email} onChange={(e) => setEmail(e.target.value)} required className="rounded-xl h-11" />
        <Input placeholder={t("login.passwordPlaceholder")} type="password" value={password} onChange={(e) => setPassword(e.target.value)} required className="rounded-xl h-11" />
        {error && <p className="text-xs text-red-500 font-medium">{error}</p>}
        <Button type="submit" className="rounded-xl h-11 font-bold" disabled={loading}>{loading ? t("login.pleaseWait") : t("login.loginButton")}</Button>
        <p className="text-sm text-muted-foreground text-center">{t("login.noAccount")} <Link className="text-primary font-semibold" to="/register">{t("login.register")}</Link></p>
      </form>
    </div>
  );
}
