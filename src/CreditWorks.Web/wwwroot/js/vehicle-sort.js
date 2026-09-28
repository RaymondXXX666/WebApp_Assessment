(() => {
    const table = document.querySelector("#vehicles-table");
    if (!table) return;

    const body = table.tBodies[0];
    const links = [...table.querySelectorAll("th a[data-sort]")];
    const columnIndex = { owner: 0, manufacturer: 1, year: 2, weight: 3 };
    const collator = new Intl.Collator(undefined, { sensitivity: "base" });

    let currentSort = table.dataset.currentSort;
    let descending = table.dataset.currentDesc === "true";

    for (const link of links) {
        link.addEventListener("click", (event) => {
            event.preventDefault();

            const selectedSort = link.dataset.sort;
            descending = selectedSort === currentSort ? !descending : false;
            currentSort = selectedSort;

            const index = columnIndex[currentSort];
            const rows = [...body.rows];

            rows.sort((left, right) => {
                const a = left.cells[index].textContent.trim();
                const b = right.cells[index].textContent.trim();

                const comparison = currentSort === "year" || currentSort === "weight"
                    ? Number(a) - Number(b)
                    : collator.compare(a, b);

                return descending ? -comparison : comparison;
            });

            body.replaceChildren(...rows);

            for (const headerLink of links) {
                const active = headerLink.dataset.sort === currentSort;
                headerLink.querySelector(".sort-indicator").textContent =
                    active ? (descending ? "▼" : "▲") : "▲";
                headerLink.closest("th").setAttribute(
                    "aria-sort",
                    active ? (descending ? "descending" : "ascending") : "none"
                );
            }

            const url = new URL(window.location.href);
            url.searchParams.set("sort", currentSort);
            if (descending) {
                url.searchParams.set("desc", "true");
            } else {
                url.searchParams.delete("desc");
            }
            window.history.replaceState(null, "", url);
        });
    }
})();
