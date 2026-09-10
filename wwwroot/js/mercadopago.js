// Integración del Card Payment Brick de MercadoPago.
// Renderiza el formulario de tarjeta; al enviarlo, tokeniza y reporta a Blazor
// (OnCardTokenized) el token + método de pago + cuotas para que el backend cree el pago.
window.mpBrick = {
    controller: null,

    async render(publicKey, amount, containerId, dotnetRef) {
        if (typeof MercadoPago === 'undefined') {
            dotnetRef.invokeMethodAsync('OnBrickError', 'No se pudo cargar el SDK de MercadoPago.');
            return;
        }
        try {
            await window.mpBrick.unmount();

            const mp = new MercadoPago(publicKey, { locale: 'es-AR' });
            const bricks = mp.bricks();

            window.mpBrick.controller = await bricks.create('cardPayment', containerId, {
                initialization: { amount: amount },
                callbacks: {
                    onReady: () => { },
                    onError: (error) => {
                        console.error('Brick error', error);
                        dotnetRef.invokeMethodAsync('OnBrickError',
                            (error && error.message) ? error.message : 'Error en el formulario de pago.');
                    },
                    onSubmit: (formData) => {
                        return new Promise((resolve) => {
                            dotnetRef.invokeMethodAsync('OnCardTokenized',
                                formData.token,
                                formData.payment_method_id,
                                formData.installments || 1);
                            resolve();
                        });
                    }
                }
            });
        } catch (e) {
            console.error('mpBrick.render', e);
            dotnetRef.invokeMethodAsync('OnBrickError', 'No se pudo inicializar el formulario de pago.');
        }
    },

    async unmount() {
        if (window.mpBrick.controller) {
            try { await window.mpBrick.controller.unmount(); } catch (e) { }
            window.mpBrick.controller = null;
        }
    }
};
