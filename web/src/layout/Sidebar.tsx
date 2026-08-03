import { Link, useLocation } from '@tanstack/react-router';
import { cn } from '@/lib/utils';
import {
  LayoutDashboard, Server, Cpu, Users, FlaskConical, Sprout, Settings2,
  Play, BarChart3, GitCompare, type LucideIcon,
} from 'lucide-react';

interface NavItem {
  label: string;
  path: string;
  icon: LucideIcon;
}

const navItems: NavItem[] = [
  { label: 'Dashboard', path: '/', icon: LayoutDashboard },
  { label: 'Providers', path: '/providers', icon: Server },
  { label: 'Models', path: '/models', icon: Cpu },
  { label: 'Model Groups', path: '/model-groups', icon: Users },
  { label: 'Tests', path: '/tests', icon: FlaskConical },
  { label: 'Seeds', path: '/seeds', icon: Sprout },
  { label: 'Configs', path: '/configs', icon: Settings2 },
  { label: 'Runs', path: '/runs', icon: Play },
  { label: 'Results', path: '/results', icon: BarChart3 },
  { label: 'Compare', path: '/compare', icon: GitCompare },
];

export function Sidebar() {
  const location = useLocation();

  return (
    <aside className="flex h-screen w-56 flex-col border-r border-border bg-sidebar">
      <div className="flex items-center gap-2 border-b border-border px-4 py-4">
        <div className="flex h-8 w-8 items-center justify-center rounded-md bg-squirmify text-squirmify-foreground font-bold text-sm">
          S
        </div>
        <span className="text-lg font-semibold tracking-tight">Squirmify</span>
      </div>

      <nav className="flex-1 space-y-1 p-3">
        {navItems.map((item) => {
          const isActive = item.path === '/'
            ? location.pathname === '/'
            : location.pathname.startsWith(item.path);

          return (
            <Link
              key={item.path}
              to={item.path}
              className={cn(
                'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                isActive
                  ? 'bg-sidebar-accent text-sidebar-accent-foreground'
                  : 'text-muted-foreground hover:bg-sidebar-accent/50 hover:text-sidebar-accent-foreground'
              )}
            >
              <item.icon className="h-4 w-4" />
              {item.label}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-border p-3 text-xs text-muted-foreground">
        Squirmify &mdash; by Firebird Solutions
      </div>
    </aside>
  );
}
