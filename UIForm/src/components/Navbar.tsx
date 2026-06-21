import { useNavigate } from "react-router-dom";
import { User, LogOut } from "lucide-react";
import { useEffect, useState } from "react";

export function Navbar() {
  const navigate = useNavigate();
  const [user, setUser] = useState<{ name: string } | null>(null);

  useEffect(() => {
    const savedUser = localStorage.getItem("user");
    if (savedUser) {
      setUser(JSON.parse(savedUser));
    }
  }, []);

  const handleLogout = () => {
    localStorage.removeItem("user");
    navigate("/");
  };

  return (
    <nav className="h-16 border-b bg-white flex items-center justify-between px-6 sticky top-0 z-10">
      <div className="text-xl font-bold text-primary">UIForm</div>
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-full bg-slate-200 flex items-center justify-center overflow-hidden">
            <User className="w-5 h-5 text-slate-500" />
          </div>
          <span className="font-medium text-slate-700">{user?.name || "User"}</span>
        </div>
        <button 
          onClick={handleLogout}
          className="p-2 hover:bg-slate-100 rounded-full transition-colors"
          title="Logout"
        >
          <LogOut className="w-5 h-5 text-slate-500" />
        </button>
      </div>
    </nav>
  );
}
