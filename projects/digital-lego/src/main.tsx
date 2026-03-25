import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createHashRouter, RouterProvider } from 'react-router-dom'
import './index.css'
import Layout from './pages/_layout'
import Dashboard from './pages/index'
import Create from './pages/create'
import Storage from './pages/storage'
import Assemble from './pages/assemble'
import MyBricks from './pages/my-bricks'
import NotFound from './pages/not-found'
import { initStore } from './lib/store'

const router = createHashRouter([
  {
    path: '/',
    element: <Layout />,
    children: [
      { index: true, element: <Dashboard /> },
      { path: 'create', element: <Create /> },
      { path: 'storage', element: <Storage /> },
      { path: 'assemble', element: <Assemble /> },
      { path: 'my-bricks', element: <MyBricks /> },
    ],
  },
  { path: '*', element: <NotFound /> },
])

async function bootstrap() {
  await initStore()
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <RouterProvider router={router} />
    </StrictMode>,
  )
}

void bootstrap()
