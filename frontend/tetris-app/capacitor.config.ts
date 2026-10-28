// Configura el contenedor Android y la carpeta web que Capacitor copia después de compilar Angular.
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
        twitter: 'compileOnly',
      },
    },
  },
};

export default config;
