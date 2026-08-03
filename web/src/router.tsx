import { createRouter, createRoute, createRootRoute } from '@tanstack/react-router';
import { Layout } from './layout/Layout';
import { DashboardPage } from './pages/DashboardPage';
import { ProvidersPage } from './pages/ProvidersPage';
import { ModelsPage } from './pages/ModelsPage';
import { TestsPage } from './pages/TestsPage';
import { SeedsPage } from './pages/SeedsPage';
import { ConfigsPage } from './pages/ConfigsPage';
import { RunsPage } from './pages/RunsPage';
import { ResultsPage } from './pages/ResultsPage';
import { ComparePage } from './pages/ComparePage';
import { ModelGroupsPage } from './pages/ModelGroupsPage';

const rootRoute = createRootRoute({ component: Layout });

const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: DashboardPage });
const providersRoute = createRoute({ getParentRoute: () => rootRoute, path: '/providers', component: ProvidersPage });
const modelsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/models', component: ModelsPage });
const modelGroupsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/model-groups', component: ModelGroupsPage });
const testsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/tests', component: TestsPage });
const seedsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/seeds', component: SeedsPage });
const configsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/configs', component: ConfigsPage });
const runsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/runs', component: RunsPage });
const resultsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/results', component: ResultsPage });
const compareRoute = createRoute({ getParentRoute: () => rootRoute, path: '/compare', component: ComparePage });

const routeTree = rootRoute.addChildren([
  indexRoute, providersRoute, modelsRoute, modelGroupsRoute, testsRoute,
  seedsRoute, configsRoute, runsRoute, resultsRoute, compareRoute,
]);

export const router = createRouter({ routeTree });

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
