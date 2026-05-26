/**
 * Reorderable list editor for ConfiguratorSection.TooltipDescription (IList<string>).
 * Uses pointer events (HTML5 drag does not work on <button> handles).
 */
(function () {
    function markFormDirty(el) {
        const form = el.closest("form");
        if (!form) {
            return;
        }
        form.classList.add("form-dirty");
        const saveBtn = form.querySelector(".button-save");
        if (saveBtn) {
            saveBtn.removeAttribute("disabled");
        }
    }

    function reindexRows(container) {
        const fieldName = container.closest("[data-tooltip-desc-list-editor]")?.dataset.fieldName;
        if (!fieldName) {
            return;
        }

        container.querySelectorAll("[data-tooltip-desc-row]").forEach((row, index) => {
            const textarea = row.querySelector("[data-tooltip-desc-value]");
            if (textarea) {
                textarea.name = `${fieldName}[${index}]`;
            }
        });
    }

    function createRow(container, readOnly, value) {
        const row = document.createElement("div");
        row.className = "tooltip-desc-row";
        row.dataset.tooltipDescRow = "true";

        if (!readOnly) {
            const handle = document.createElement("span");
            handle.className = "tooltip-desc-handle";
            handle.dataset.tooltipDescHandle = "true";
            handle.title = "Drag to reorder";
            handle.setAttribute("role", "button");
            handle.setAttribute("tabindex", "0");
            handle.setAttribute("aria-label", "Drag to reorder");
            handle.textContent = "⋮⋮";
            row.appendChild(handle);
        }

        const textarea = document.createElement("textarea");
        textarea.className = "form-control form-control-sm";
        textarea.rows = 2;
        textarea.dataset.tooltipDescValue = "true";
        textarea.value = value ?? "";
        if (readOnly) {
            textarea.readOnly = true;
        }
        row.appendChild(textarea);

        if (!readOnly) {
            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "btn btn-sm btn-outline-danger";
            remove.dataset.tooltipDescRemove = "true";
            remove.title = "Remove paragraph";
            remove.textContent = "×";
            row.appendChild(remove);
        }

        return row;
    }

    function rowAtPoint(container, clientY) {
        const rows = [...container.querySelectorAll("[data-tooltip-desc-row]")];
        for (const row of rows) {
            const rect = row.getBoundingClientRect();
            if (clientY >= rect.top && clientY <= rect.bottom) {
                return row;
            }
        }
        return null;
    }

    function insertRowAtPointer(container, draggedRow, clientY) {
        const target = rowAtPoint(container, clientY);
        if (!target || target === draggedRow) {
            return;
        }

        const rect = target.getBoundingClientRect();
        const after = clientY > rect.top + rect.height / 2;
        if (after) {
            target.after(draggedRow);
        } else {
            target.before(draggedRow);
        }
    }

    function wirePointerReorder(container, readOnly) {
        if (readOnly) {
            return;
        }

        let draggedRow = null;

        const onMove = (event) => {
            if (!draggedRow) {
                return;
            }
            event.preventDefault();
            insertRowAtPointer(container, draggedRow, event.clientY);
        };

        const onEnd = () => {
            if (!draggedRow) {
                return;
            }
            draggedRow.classList.remove("tooltip-desc-row-dragging");
            container.querySelectorAll("[data-tooltip-desc-row]").forEach((row) => {
                row.classList.remove("tooltip-desc-row-drop-target");
            });
            draggedRow = null;
            document.body.classList.remove("tooltip-desc-list-dragging");
            document.removeEventListener("mousemove", onMove);
            document.removeEventListener("mouseup", onEnd);
            reindexRows(container);
            markFormDirty(container);
        };

        container.addEventListener("mousedown", (event) => {
            if (event.button !== 0) {
                return;
            }

            const handle = event.target.closest("[data-tooltip-desc-handle]");
            if (!handle) {
                return;
            }

            draggedRow = handle.closest("[data-tooltip-desc-row]");
            if (!draggedRow) {
                return;
            }

            event.preventDefault();
            draggedRow.classList.add("tooltip-desc-row-dragging");
            document.body.classList.add("tooltip-desc-list-dragging");
            document.addEventListener("mousemove", onMove);
            document.addEventListener("mouseup", onEnd);
        });

        container.addEventListener("mousemove", (event) => {
            if (!draggedRow) {
                return;
            }
            const target = rowAtPoint(container, event.clientY);
            container.querySelectorAll("[data-tooltip-desc-row]").forEach((row) => {
                row.classList.toggle("tooltip-desc-row-drop-target", row === target && row !== draggedRow);
            });
        });
    }

    function initEditor(editor) {
        if (editor.dataset.initialized === "true") {
            return;
        }
        editor.dataset.initialized = "true";

        const readOnly = editor.dataset.readonly === "true";
        const container = editor.querySelector("[data-tooltip-desc-rows]");
        if (!container) {
            return;
        }

        wirePointerReorder(container, readOnly);

        editor.querySelector("[data-tooltip-desc-add]")?.addEventListener("click", () => {
            const row = createRow(container, readOnly, "");
            container.appendChild(row);
            reindexRows(container);
            row.querySelector("[data-tooltip-desc-value]")?.focus();
            markFormDirty(container);
        });

        container.addEventListener("click", (event) => {
            const removeBtn = event.target.closest("[data-tooltip-desc-remove]");
            if (!removeBtn) {
                return;
            }
            removeBtn.closest("[data-tooltip-desc-row]")?.remove();
            reindexRows(container);
            markFormDirty(container);
        });

        container.addEventListener("input", (event) => {
            if (event.target.matches("[data-tooltip-desc-value]")) {
                markFormDirty(container);
            }
        });

        reindexRows(container);
    }

    function initAll() {
        document.querySelectorAll("[data-tooltip-desc-list-editor]").forEach(initEditor);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAll);
    } else {
        initAll();
    }

    // Tabbed edit forms inject fields after first paint.
    let initTimer;
    const observer = new MutationObserver(() => {
        clearTimeout(initTimer);
        initTimer = setTimeout(initAll, 50);
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();
