import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
    testDir: './',
    fullyParallel: false,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 2 : 0,
    workers: 1,
    reporter: 'html',

    use: {
        baseURL: 'http://localhost:3001', // Admin frontend
        trace: 'on-first-retry',
        screenshot: 'only-on-failure',
    },

    projects: [
        {
            name: 'chromium',
            use: { ...devices['Desktop Chrome'] },
        },
    ],

    // Admin frontend dev server
    webServer: {
        command: 'npm run dev',
        url: 'http://localhost:3001',
        cwd: '../../frontend/admin',
        reuseExistingServer: true,
        timeout: 30_000,
    },
});
