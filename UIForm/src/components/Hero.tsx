import { Button } from "@/components/ui/button";

export function Hero() {
  return (
    <div className="relative h-[500px] w-full overflow-hidden rounded-xl bg-slate-100">
      <img
        src="https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&q=80&w=2426&ixlib=rb-4.0.3"
        alt="Hero Background"
        className="absolute inset-0 w-full h-full object-cover"
      />
      <div className="absolute inset-0 bg-slate-900/40" />
      <div className="relative h-full flex flex-col items-center justify-center text-center px-6">
        <h2 className="text-4xl md:text-6xl font-bold text-white mb-6">
          Build Forms Faster than Ever
        </h2>
        <p className="text-lg md:text-xl text-slate-100 max-w-2xl mb-8">
          The most intuitive way to create, manage, and analyze your web forms. 
          Start collecting data today with our powerful integration tools.
        </p>
        <div className="flex gap-4">
          <Button size="lg" className="px-8">Get Started</Button>
          <Button size="lg" variant="outline" className="px-8 bg-white/10 text-white border-white/20 hover:bg-white/20">
            Learn More
          </Button>
        </div>
      </div>
    </div>
  );
}
