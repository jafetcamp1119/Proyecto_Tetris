// Integra el WebView de Tetris con el resultado del acceso Google.
// Limita depuración y contenido mixto según si la aplicación es debug o release.
package com.proyectotetris.juegocr;

import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.os.Bundle;
import android.webkit.WebSettings;

import com.getcapacitor.BridgeActivity;
import com.getcapacitor.PluginHandle;

import ee.forgr.capacitor.social.login.GoogleProvider;
import ee.forgr.capacitor.social.login.ModifiedMainActivityForSocialLoginPlugin;
import ee.forgr.capacitor.social.login.SocialLoginPlugin;

public class MainActivity extends BridgeActivity implements ModifiedMainActivityForSocialLoginPlugin {
    @Override
    // Prepara el WebView y permite depuración y contenido mixto únicamente en la variante depurable.
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        android.webkit.WebView.setWebContentsDebuggingEnabled(
                (getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) != 0);

        if ((getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) != 0) {
            // HTTP local solo en desarrollo; la política de red limita la IP permitida.
            getBridge().getWebView().getSettings().setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        } else {
            getBridge().getWebView().getSettings().setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        }
    }

    @Override
    // Entrega al plugin el resultado de las solicitudes Google que reconoce su rango de códigos.
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode >= GoogleProvider.REQUEST_AUTHORIZE_GOOGLE_MIN
                && requestCode < GoogleProvider.REQUEST_AUTHORIZE_GOOGLE_MAX) {
            PluginHandle manejador = getBridge().getPlugin("SocialLogin");
            if (manejador != null && manejador.getInstance() instanceof SocialLoginPlugin) {
                ((SocialLoginPlugin) manejador.getInstance()).handleGoogleLoginIntent(requestCode, data);
            }
        }
    }

    @Override
    public void IHaveModifiedTheMainActivityForTheUseWithSocialLoginPlugin() {
        // Marcador requerido por el plugin para comprobar la integración de Android.
    }
}
