import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/components/auth/AuthProvider";

export default function Register() {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [age, setAge] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const auth = useAuth();
  const navigate = useNavigate();

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");

    if (password !== confirmPassword) {
      setError("Password and confirm password must match.");
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
        age: age ? Number(age) : null,
      });
      navigate("/login");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Registration failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="h-full flex items-center justify-center p-6">
      <form onSubmit={onSubmit} className="w-full max-w-sm bg-white rounded-[2rem] border border-border p-6 flex flex-col gap-3 shadow-sm">
        <h2 className="text-2xl font-bold">Register</h2>
        <Input placeholder="First name" value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
        <Input placeholder="Last name" value={lastName} onChange={(e) => setLastName(e.target.value)} required />
        <Input placeholder="Age" type="number" min={1} value={age} onChange={(e) => setAge(e.target.value)} />
        <Input placeholder="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        <Input placeholder="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        <Input placeholder="Confirm password" type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} required />
        {error && <p className="text-xs text-red-600">{error}</p>}
        <Button type="submit" className="rounded-xl" disabled={loading}>{loading ? "Please wait..." : "Create Account"}</Button>
        <p className="text-sm text-muted-foreground">Already have an account? <Link className="text-primary font-semibold" to="/login">Login</Link></p>
      </form>
    </div>
  );
}
