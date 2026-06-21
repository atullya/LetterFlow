import { Navbar } from "./Navbar";
import { Sidebar } from "./Sidebar";
import { Hero } from "./Hero";

export function Home() {
  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      <Navbar />
      <div className="flex flex-1">
        <Sidebar />
        <main className="flex-1 p-8 overflow-y-auto">
          <div className="max-w-6xl mx-auto space-y-8">
            <header>
              <h1 className="text-3xl font-bold text-slate-900">Dashboard</h1>
              <p className="text-slate-500">Welcome back! Here's what's happening with your forms.</p>
            </header>
            
            <Hero />
            
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {[1, 2, 3].map((i) => (
                <div key={i} className="bg-white p-6 rounded-xl border shadow-sm">
                  <h3 className="font-semibold text-slate-700 mb-2">Form Stat {i}</h3>
                  <p className="text-3xl font-bold text-slate-900">{Math.floor(Math.random() * 1000)}</p>
                  <p className="text-sm text-green-600 font-medium mt-1">+12% from last month</p>
                </div>
              ))}
            </div>
          </div>
        </main>
      </div>
    </div>
  );
}
