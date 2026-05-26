/**
 * Fixed-key editor for ConfiguratorMessages.MessagesJson (key → string object).
 */
(function () {
    function parseJson(value, fallback) {
        if (!value || !String(value).trim()) {
            return fallback;
        }
        try {
            return JSON.parse(value);
        } catch {
            return fallback;
        }
    }

    function findMessagesJsonInput(form) {
        if (!form) {
            return null;
        }
        return (
            form.querySelector('input[data-messages-json]') ||
            form.querySelector('input[name="MessagesJson"]') ||
            form.querySelector('input[name$=".MessagesJson"]')
        );
    }

    function markFormDirty(input) {
        const form = input.closest("form");
        if (!form) {
            return;
        }
        form.classList.add("form-dirty");
        const saveBtn = form.querySelector(".button-save");
        if (saveBtn) {
            saveBtn.removeAttribute("disabled");
        }
        input.dispatchEvent(new Event("change", { bubbles: true }));
        input.dispatchEvent(new Event("input", { bubbles: true }));
    }

    function entriesToMessagesObject(entries) {
        const messages = {};
        entries.forEach((entry) => {
            if (entry?.key) {
                messages[entry.key] = entry.value ?? "";
            }
        });
        return messages;
    }

    function messagesObjectToEntries(messages) {
        if (!messages || typeof messages !== "object" || Array.isArray(messages)) {
            return [];
        }
        return Object.keys(messages).map((key) => ({
            key,
            value: messages[key] ?? "",
        }));
    }

    function syncHidden(hidden, entries) {
        hidden.value = JSON.stringify(entriesToMessagesObject(entries));
        markFormDirty(hidden);
    }

    function groupKey(key) {
        const parts = key.split(".");
        if (parts.length <= 2) {
            return key;
        }
        if (parts[1] === "components" && parts.length >= 4) {
            return parts.slice(0, 4).join(".");
        }
        return parts.slice(0, 3).join(".");
    }

    function groupLabel(groupKeyName) {
        return groupKeyName.replace(/^app\./, "").replace(/\./g, " › ");
    }

    function buildEntriesFromTemplate(template, stored) {
        return Object.keys(template).map((key) => ({
            key,
            value: Object.prototype.hasOwnProperty.call(stored, key) ? stored[key] : template[key],
        }));
    }

    function hydrateEntries(editor, hidden) {
        const stored = parseJson(hidden.value, {});
        let entries = messagesObjectToEntries(stored);

        if (entries.length > 0) {
            return entries;
        }

        const templateNode = editor.querySelector(".configurator-messages-template-json");
        const template = parseJson(templateNode?.textContent, {});
        entries = buildEntriesFromTemplate(template, stored);
        syncHidden(hidden, entries);
        return entries;
    }

    function renderGroup(container, groupId, groupTitle, entries, readOnly, onChange) {
        const item = document.createElement("div");
        item.className = "accordion-item";

        const headerId = `msg-group-${groupId}`;
        const collapseId = `msg-collapse-${groupId}`;

        item.innerHTML = `
            <h2 class="accordion-header" id="${headerId}">
                <button class="accordion-button collapsed" type="button" data-bs-toggle="collapse"
                        data-bs-target="#${collapseId}" aria-expanded="false" aria-controls="${collapseId}">
                    <span class="configurator-messages-group-title">${groupTitle}</span>
                    <span class="badge bg-secondary ms-2">${entries.length}</span>
                </button>
            </h2>
            <div id="${collapseId}" class="accordion-collapse collapse" aria-labelledby="${headerId}">
                <div class="accordion-body p-0">
                    <table class="table table-sm table-bordered mb-0 configurator-messages-table">
                        <thead class="table-light">
                            <tr>
                                <th class="configurator-messages-key-col">Key</th>
                                <th>Value</th>
                            </tr>
                        </thead>
                        <tbody></tbody>
                    </table>
                </div>
            </div>`;

        const tbody = item.querySelector("tbody");
        entries.forEach((entry) => {
            const tr = document.createElement("tr");
            tr.dataset.messageKey = entry.key;
            tr.dataset.searchText = `${entry.key} ${entry.value}`.toLowerCase();

            const keyTd = document.createElement("td");
            keyTd.className = "configurator-messages-key-col";
            const code = document.createElement("code");
            code.className = "small text-muted";
            code.textContent = entry.key;
            keyTd.appendChild(code);

            const valueTd = document.createElement("td");
            const textarea = document.createElement("textarea");
            textarea.className = "form-control form-control-sm";
            textarea.rows = entry.value && entry.value.length > 120 ? 4 : 2;
            textarea.value = entry.value ?? "";
            textarea.dataset.role = "value";
            if (readOnly) {
                textarea.readOnly = true;
            }
            textarea.addEventListener("input", () => {
                entry.value = textarea.value;
                tr.dataset.searchText = `${entry.key} ${entry.value}`.toLowerCase();
                onChange();
            });
            valueTd.appendChild(textarea);

            tr.appendChild(keyTd);
            tr.appendChild(valueTd);
            tbody.appendChild(tr);
        });

        container.appendChild(item);
        return item;
    }

    function collectEntries(editor) {
        const entries = [];
        editor.querySelectorAll("tbody tr[data-message-key]").forEach((tr) => {
            const key = tr.dataset.messageKey;
            const value = tr.querySelector('[data-role="value"]')?.value ?? "";
            entries.push({ key, value });
        });
        return entries;
    }

    function applyFilter(editor, query) {
        const q = (query ?? "").trim().toLowerCase();
        editor.querySelectorAll(".accordion-item").forEach((group) => {
            let visibleInGroup = 0;
            group.querySelectorAll("tbody tr[data-message-key]").forEach((tr) => {
                const show = !q || (tr.dataset.searchText ?? "").includes(q);
                tr.classList.toggle("d-none", !show);
                if (show) {
                    visibleInGroup++;
                }
            });
            group.classList.toggle("d-none", visibleInGroup === 0);
            if (q && visibleInGroup > 0) {
                const collapse = group.querySelector(".accordion-collapse");
                collapse?.classList.add("show");
                group.querySelector(".accordion-button")?.classList.remove("collapsed");
            }
        });
    }

    function initEditor(editor) {
        if (editor.dataset.initialized === "true") {
            return;
        }
        editor.dataset.initialized = "true";

        const hidden = editor.querySelector("[data-messages-json]");
        if (!hidden) {
            return;
        }

        const readOnly = editor.dataset.readonly === "true";
        const groupsRoot = editor.querySelector("[data-messages-groups]");
        const filterInput = editor.querySelector("[data-messages-filter]");

        const entries = hydrateEntries(editor, hidden);
        const grouped = new Map();
        entries.forEach((entry) => {
            const g = groupKey(entry.key);
            if (!grouped.has(g)) {
                grouped.set(g, []);
            }
            grouped.get(g).push(entry);
        });

        const onChange = () => syncHidden(hidden, collectEntries(editor));

        let groupIndex = 0;
        [...grouped.keys()].sort((a, b) => a.localeCompare(b)).forEach((g) => {
            renderGroup(groupsRoot, groupIndex++, groupLabel(g), grouped.get(g), readOnly, onChange);
        });

        filterInput?.addEventListener("input", () => applyFilter(editor, filterInput.value));
    }

    function flushMessageEditors() {
        document.querySelectorAll("[data-configurator-messages-editor]").forEach((editor) => {
            const hidden = editor.querySelector("[data-messages-json]");
            if (!hidden) {
                return;
            }
            syncHidden(hidden, collectEntries(editor));
        });
    }

    function wireFlushHandlers() {
        document.querySelectorAll("form.form-edit, #new-form").forEach((form) => {
            form.addEventListener("submit", flushMessageEditors, true);
        });

        document.addEventListener(
            "click",
            (event) => {
                if (event.target.closest(".button-save")) {
                    flushMessageEditors();
                }
            },
            true
        );
    }

    function initAll() {
        document.querySelectorAll("[data-configurator-messages-editor]").forEach(initEditor);
        wireFlushHandlers();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAll);
    } else {
        initAll();
    }
})();
