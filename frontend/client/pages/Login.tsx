import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/components/auth/AuthProvider";

export default function Login() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const auth = useAuth();
  const navigate = useNavigate();

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await auth.login(email, password);
      navigate("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="h-full flex items-center justify-center p-6">
      <form onSubmit={onSubmit} className="w-full max-w-sm bg-white rounded-[2rem] border border-border p-6 flex flex-col gap-4 shadow-sm">
        <h2 className="text-2xl font-bold">Login</h2>
        <Input placeholder="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        <Input placeholder="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        {error && <p className="text-xs text-red-600">{error}</p>}
        <Button type="submit" className="rounded-xl" disabled={loading}>{loading ? "Please wait..." : "Login"}</Button>
        <p className="text-sm text-muted-foreground">No account? <Link className="text-primary font-semibold" to="/register">Register</Link></p>
      </form>
    </div>
  );
}
