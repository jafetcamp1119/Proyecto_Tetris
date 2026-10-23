import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.proyectotetris.juegocr',
  appName: 'Tetris',
  webDir: 'www',
  plugins: {
    SocialLogin: {
      providers: {
        google: 'implementation',
        facebook: 'compileOnly',
        apple: 'compileOnly',
        twitter: 'compileOnly'
      }
    }
  }
};

export default config;
