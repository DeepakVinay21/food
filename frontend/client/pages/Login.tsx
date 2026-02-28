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
      <form onSubmit={onSubmit} className="w-full max-w-sm bg-card rounded-[2rem] border border-border p-6 flex flex-col gap-4 shadow-lg">
        <h2 className="text-2xl font-bold text-foreground">Welcome Back</h2>
        <p className="text-sm text-muted-foreground -mt-2">Sign in to your account</p>
        <Input placeholder="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required className="rounded-xl h-11" />
        <Input placeholder="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required className="rounded-xl h-11" />
        {error && <p className="text-xs text-red-500 font-medium">{error}</p>}
        <Button type="submit" className="rounded-xl h-11 font-bold" disabled={loading}>{loading ? "Please wait..." : "Login"}</Button>
        <p className="text-sm text-muted-foreground text-center">No account? <Link className="text-primary font-semibold" to="/register">Register</Link></p>
      </form>
    </div>
  );
}
