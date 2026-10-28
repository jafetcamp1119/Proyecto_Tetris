import { afterEach, expect, it, vi } from 'vitest';
import { RepetidorEntrada } from './repetidor-entrada';
afterEach(() => vi.useRealTimers());

it('teclado y táctil comparten 230 ms iniciales y 100 ms de repetición; soltar detiene todo', () => {
    vi.useFakeTimers();
    const emitir = vi.fn();
    const entrada = new RepetidorEntrada(emitir, () => true);
    entrada.presionar('Izquierda');
    expect(emitir).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(229);
    expect(emitir).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(201);
    expect(emitir).toHaveBeenCalledTimes(4);
    entrada.soltar();
    vi.advanceTimersByTime(1000);
    expect(emitir).toHaveBeenCalledTimes(4);
});

it('no repite rotación/caída ni conserva entradas después de perder el control', () => {
    vi.useFakeTimers();
    let habilitado = true;
    const emitir = vi.fn();
    const entrada = new RepetidorEntrada(emitir, () => habilitado);
    entrada.presionar('Caer');
    vi.advanceTimersByTime(1000);
    expect(emitir).toHaveBeenCalledTimes(1);
    entrada.presionar('Bajar');
    habilitado = false;
    vi.advanceTimersByTime(1000);
    expect(emitir).toHaveBeenCalledTimes(2);
});
