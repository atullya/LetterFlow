import { LayoutDashboard, Users, Settings, HelpCircle, FileText } from "lucide-react";
import { cn } from "@/lib/utils";

const menuItems = [
  { icon: LayoutDashboard, label: "Dashboard", active: true },
  { icon: FileText, label: "Forms" },
  { icon: Users, label: "Submissions" },
  { icon: Settings, label: "Settings" },
  { icon: HelpCircle, label: "Support" },
];

export function Sidebar() {
  return (
    <aside className="w-64 bg-slate-900 text-slate-300 flex flex-col h-[calc(100vh-64px)] sticky top-16">
      <div className="p-4 flex-1">
        <ul className="space-y-2">
          {menuItems.map((item) => (
            <li key={item.label}>
              <button
                className={cn(
                  "w-full flex items-center gap-3 px-3 py-2 rounded-md transition-colors text-sm font-medium",
                  item.active 
                    ? "bg-slate-800 text-white" 
                    : "hover:bg-slate-800 hover:text-white"
                )}
              >
                <item.icon className="w-5 h-5" />
                {item.label}
              </button>
            </li>
          ))}
        </ul>
      </div>
      <div className="p-4 border-t border-slate-800">
        <p className="text-xs text-slate-500">© 2026 UIForm Inc.</p>
      </div>
    </aside>
  );
}
