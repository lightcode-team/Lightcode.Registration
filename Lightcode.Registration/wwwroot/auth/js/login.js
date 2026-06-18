(function () {
    const form = document.querySelector("[data-login-form]");

    if (!form) {
        return;
    }

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
})();
