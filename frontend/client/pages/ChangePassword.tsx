import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/components/auth/AuthProvider";
import { api } from "@/lib/api";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "@/lib/i18n/LanguageContext";

export default function ChangePassword() {
  const [oldPassword, setOldPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [ok, setOk] = useState("");
  const [loading, setLoading] = useState(false);
  const { token } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation();

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setOk("");

    if (newPassword !== confirmPassword) {
      setError(t("changePassword.passwordsMustMatch"));
      return;
    }

    setLoading(true);
    try {
      await api.changePassword(token!, oldPassword, newPassword);
      setOk(t("changePassword.success"));
      setTimeout(() => navigate("/profile"), 800);
    } catch (err) {
      setError(err instanceof Error ? err.message : t("changePassword.failed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="p-6 flex justify-center">
      <form onSubmit={submit} className="w-full max-w-md bg-white dark:bg-card border rounded-2xl p-5 grid gap-3">
        <h2 className="text-xl font-bold">{t("changePassword.title")}</h2>
        <Input type="password" placeholder={t("changePassword.oldPasswordPlaceholder")} value={oldPassword} onChange={(e) => setOldPassword(e.target.value)} required />
        <Input type="password" placeholder={t("changePassword.newPasswordPlaceholder")} value={newPassword} onChange={(e) => setNewPassword(e.target.value)} required />
        <Input type="password" placeholder={t("changePassword.confirmNewPasswordPlaceholder")} value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} required />
        {error && <p className="text-xs text-red-600">{error}</p>}
        {ok && <p className="text-xs text-green-600">{ok}</p>}
        <Button type="submit" disabled={loading}>{loading ? t("changePassword.updating") : t("changePassword.updatePassword")}</Button>
      </form>
    </div>
  );
}
