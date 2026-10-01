(() => {
    function initializeCategories() {
        const rows = document.querySelector("#category-rows");
        const template = document.querySelector("#category-row-template");
        const addButton = document.querySelector("#add-category");

        if (!rows || !template || !addButton || rows.dataset.initialized) return;
        rows.dataset.initialized = "true";

        const allowedIcons = new Set(["light.svg", "medium.svg", "heavy.svg"]);

        function updatePreview(row) {
            const picker = row.querySelector(".category-icon-picker");
            const select = picker?.querySelector("select");
            const preview = picker?.querySelector("img");
            if (!select || !preview) return;

            const icon = allowedIcons.has(select.value) ? select.value : "light.svg";
            preview.src = picker.dataset.iconBase + icon;
        }

        function reindex() {
            [...rows.querySelectorAll(".category-row")].forEach((row, index) => {
                row.querySelectorAll("[name]").forEach(field => {
                    field.name = field.name.replace(
                        /Categories\[(?:\d+|__INDEX__)\]/,
                        `Categories[${index}]`
                    );
                });
            });
        }

        rows.querySelectorAll(".category-row").forEach(updatePreview);

        rows.addEventListener("change", event => {
            if (event.target.matches(".category-icon-select")) {
                updatePreview(event.target.closest(".category-row"));
            }
        });

        addButton.addEventListener("click", () => {
            const fragment = template.content.cloneNode(true);
            const row = fragment.querySelector(".category-row");
            rows.appendChild(fragment);
            reindex();
            updatePreview(row);
            row.querySelector('[name$=".Name"]').focus();
        });

        rows.addEventListener("click", event => {
            const button = event.target.closest(".remove-category");
            if (!button) return;
            button.closest(".category-row").remove();
            reindex();
        });
    }

    document.addEventListener("creditworks:page-loaded", initializeCategories);
    initializeCategories();
})();
