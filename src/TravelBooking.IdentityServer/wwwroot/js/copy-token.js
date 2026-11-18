(function () {
    var button = document.getElementById('copy-token');
    var token = document.getElementById('access-token');
    var feedback = document.getElementById('copy-feedback');

    if (!button || !token) {
        return;
    }

    button.addEventListener('click', function () {
        token.select();

        var done = function () { feedback.textContent = 'Copied.'; };
        var failed = function () { feedback.textContent = 'Copy failed — select the text and copy manually.'; };

        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(token.value).then(done, failed);
        } else if (document.execCommand('copy')) {
            done();
        } else {
            failed();
        }
    });
})();
