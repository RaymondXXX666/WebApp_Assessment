(() => {
    let pendingRequest;

    async function loadPage(url, addHistory) {
        pendingRequest?.abort();
        const request = new AbortController();
        pendingRequest = request;

        try {
            const response = await fetch(url, {
                signal: request.signal,
                headers: { "X-Requested-With": "fetch" }
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const html = await response.text();
            const page = new DOMParser().parseFromString(html, "text/html");
            const nextMain = page.querySelector("main[role='main']");
            const currentMain = document.querySelector("main[role='main']");
            if (!nextMain || !currentMain) throw new Error("Page content missing");

            currentMain.replaceWith(document.importNode(nextMain, true));
            document.dispatchEvent(new Event("creditworks:page-loaded"));
            document.title = page.title;

            if (addHistory) history.pushState(null, "", url);
            window.scrollTo(0, 0);

            // Scripts inside fetched HTML do not execute automatically.
            document.querySelectorAll("script[data-page-script]").forEach(script => script.remove());

            const pageScripts = [...page.querySelectorAll("script[src]")]
                .filter(script => /\/js\/vehicle-sort\.js(?:\?|$)/.test(
                    new URL(script.getAttribute("src"), url).pathname
                ));

            for (const original of pageScripts) {
                const script = document.createElement("script");
                script.src = new URL(original.getAttribute("src"), url).href;
                script.dataset.pageScript = "true";
                await new Promise((resolve, reject) => {
                    script.onload = resolve;
                    script.onerror = reject;
                    document.body.appendChild(script);
                });
            }
        } catch (error) {
            if (error.name !== "AbortError") window.location.assign(url);
        } finally {
            if (pendingRequest === request) pendingRequest = null;
        }
    }

    document.addEventListener("click", event => {
        const link = event.target.closest("a[data-swap-page]");
        if (!link || event.defaultPrevented || event.button !== 0 ||
            event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

        const url = new URL(link.href);
        if (url.origin !== location.origin || url.hash) return;

        event.preventDefault();
        loadPage(url.href, true);
    });

    window.addEventListener("popstate", () => loadPage(location.href, false));
})();
