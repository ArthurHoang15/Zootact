import { StrictMode, Suspense } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import './i18n';
import App from './App';
import { upgradeLegacyHashRoute } from './router/legacyHash';
import './index.css';

upgradeLegacyHashRoute();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Suspense
      fallback={(
        <div className="min-h-screen bg-cream flex items-center justify-center">
          <div className="text-center">
            <div className="mb-4 text-6xl animate-bounce">Paw</div>
            <p className="font-display text-xl text-candy-green">Loading...</p>
          </div>
        </div>
      )}
    >
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </Suspense>
  </StrictMode>
);
