(() => {
    const rows = document.querySelector("#category-rows");
    const template = document.querySelector("#category-row-template");
    const addButton = document.querySelector("#add-category");

    if (!rows || !template || !addButton) return;

    function reindex() {
        [...rows.querySelectorAll(".category-row")].forEach((row, index) => {
            row.querySelectorAll("[name]").forEach((field) => {
                field.name = field.name.replace(
                    /Categories\[\d+\]/,
                    `Categories[${index}]`
                );
            });
        });
    }

    addButton.addEventListener("click", () => {
        const index = rows.querySelectorAll(".category-row").length;
        rows.insertAdjacentHTML(
            "beforeend",
            template.innerHTML.replaceAll("__INDEX__", String(index))
        );
        reindex();
    });

    rows.addEventListener("click", (event) => {
        const button = event.target.closest(".remove-category");
        if (!button) return;

        button.closest(".category-row").remove();
        reindex();
    });
})();
