import { Injectable } from '@angular/core';
import { ILogger, LogLevel } from '@microsoft/signalr';

@Injectable({ providedIn: 'root' })
export class RendimientoServicio implements ILogger {
    readonly habilitado: boolean;
    private readonly tiempos = new Map<string, number[]>();
    private readonly contadores = new Map<string, number>();
    private transporte = 'Sin comprobar';
    private transporteSeleccionado = 'Sin comprobar';
    private entrada = 0;
    private entradaPintada = 0;

    marcarEntrada(): void {
        if (this.habilitado) {
            const ahora = performance.now();
            if (this.entrada) { this.muestra('intervalo_entradas_ms', ahora - this.entrada); }
            this.entrada = ahora;
        }
    }

    registrarEvento(desde: number): void {
        if (this.habilitado && Math.abs(performance.now() - desde) < 10000) { this.finalizar('evento_procesamiento_ms', desde); }
    }

    maximo(nombre: string, valor: number): void {
        if (this.habilitado) { this.contadores.set(nombre, Math.max(valor, this.contadores.get(nombre) ?? 0)); }
    }

    marcarDibujo(): void {
        if (this.habilitado && this.entrada > this.entradaPintada) {
            this.finalizar('entrada_visual_ms', this.entrada);
            this.entradaPintada = this.entrada;
        }
    }

    constructor() {
        this.habilitado = this.leerPreferencia();
        if (this.habilitado) {
            Object.assign(window, { tetrisRendimiento: { resumen: () => this.resumen() } });
        }
    }

    iniciar(): number {
        return this.habilitado ? performance.now() : 0;
    }

    finalizar(nombre: string, inicio: number): void {
        if (!this.habilitado) { return; }
        this.muestra(nombre, performance.now() - inicio);
    }

    muestra(nombre: string, valor: number): void {
        if (!this.habilitado) { return; }
        const valores = this.tiempos.get(nombre) ?? [];
        valores.push(valor);
        if (valores.length > 120) { valores.shift(); }
        this.tiempos.set(nombre, valores);
    }

    contar(nombre: string): void {
        if (this.habilitado) { this.contadores.set(nombre, (this.contadores.get(nombre) ?? 0) + 1); }
    }

    log(_nivel: LogLevel, mensaje: string): void {
        // SignalR puede incluir tokens en los mensajes: nunca conservar ni imprimir el texto.
        if (!this.habilitado) { return; }
        if (mensaje.includes("Selecting transport 'WebSockets'")) { this.transporteSeleccionado = 'WebSockets'; }
        if (mensaje.includes("Selecting transport 'LongPolling'")) { this.transporteSeleccionado = 'LongPolling'; }
        if (mensaje.includes('WebSocket connected')) { this.transporte = 'WebSockets'; }
        if (mensaje === 'The HttpConnection connected successfully.') { this.transporte = this.transporteSeleccionado; }
        if (mensaje.startsWith('Connection disconnected')) { this.transporte = 'Desconectado'; }
    }

    resumen(): object {
        const tiempos: Record<string, object> = {};
        for (const [nombre, valores] of this.tiempos) {
            const ordenados = [...valores].sort((a, b) => a - b);
            tiempos[nombre] = {
                muestras: valores.length, p50Ms: ordenados[Math.floor((ordenados.length - 1) * 0.5)],
                p95Ms: ordenados[Math.floor((ordenados.length - 1) * 0.95)], maxMs: ordenados[ordenados.length - 1]
            };
        }
        return { transporte: this.transporte, contadores: Object.fromEntries(this.contadores), tiempos };
    }

    private leerPreferencia(): boolean {
        try { return localStorage.getItem('tetris.diagnostico.rendimiento') === '1'; }
        catch { return false; }
    }
}
