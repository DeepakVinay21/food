import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/components/auth/AuthProvider";
import { api } from "@/lib/api";
import { useNavigate } from "react-router-dom";

export default function ProfileEdit() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [age, setAge] = useState("");
  const [photo, setPhoto] = useState<string | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    api.profile(token!).then((p) => {
      setFirstName(p.firstName || "");
      setLastName(p.lastName || "");
      setAge(p.age ? String(p.age) : "");
      setPhoto(p.profilePhotoDataUrl ?? null);
    }).catch(() => {});
  }, [token]);

  const onPickPhoto = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => setPhoto(reader.result as string);
    reader.readAsDataURL(file);
  };

  const onSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      await api.updateProfile(token!, {
        firstName,
        lastName,
        age: age ? Number(age) : null,
        profilePhotoDataUrl: photo,
      });
      navigate("/profile");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save profile");
    }
  };

  return (
    <div className="p-6 flex justify-center">
      <form onSubmit={onSave} className="w-full max-w-md bg-white dark:bg-card border rounded-2xl p-5 grid gap-3">
        <h2 className="text-xl font-bold">Edit Profile</h2>
        {photo ? <img src={photo} alt="profile" className="w-24 h-24 rounded-2xl object-cover border" /> : <div className="w-24 h-24 rounded-2xl border grid place-items-center text-xs text-muted-foreground">No photo</div>}
        <Input type="file" accept="image/*" onChange={onPickPhoto} />
        <Input placeholder="First name" value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
        <Input placeholder="Last name" value={lastName} onChange={(e) => setLastName(e.target.value)} required />
        <Input placeholder="Age" type="number" min={1} value={age} onChange={(e) => setAge(e.target.value)} />
        {error && <p className="text-xs text-red-600">{error}</p>}
        <Button type="submit">Save Profile</Button>
      </form>
    </div>
  );
}
