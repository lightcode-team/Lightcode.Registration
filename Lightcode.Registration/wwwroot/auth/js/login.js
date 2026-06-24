(function () {
    document.querySelectorAll("[data-login-form]").forEach(function (form) {
        const username = form.querySelector("[data-login-username]");
        const submit = form.querySelector("[data-login-submit]");

        form.addEventListener("submit", function () {
            if (username) {
                username.value = username.value.trim();
            }

            if (submit && form.checkValidity()) {
                submit.disabled = true;
                submit.textContent = submit.dataset.loadingText || submit.textContent;
            }
        });
    });

    const password = document.querySelector("[data-password-input]");
    const passwordToggle = document.querySelector("[data-password-toggle]");
    if (password && passwordToggle) {
        passwordToggle.addEventListener("click", function () {
            const visible = password.type === "text";
            password.type = visible ? "password" : "text";
            passwordToggle.textContent = visible ? "Mostrar" : "Ocultar";
        });
    }

    const resend = document.querySelector("[data-resend-button]");
    if (resend) {
        let remaining = Number(resend.dataset.cooldown || 0);
        const originalText = resend.textContent;
        const tick = function () {
            resend.disabled = remaining > 0;
            resend.textContent = remaining > 0 ? `Reenviar em ${remaining}s` : originalText;
            remaining -= 1;
            if (remaining >= 0) {
                window.setTimeout(tick, 1000);
            }
        };
        tick();
    }
})();
