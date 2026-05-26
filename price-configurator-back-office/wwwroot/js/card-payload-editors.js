/**
 * Visual editors for ConfiguratorCard JSON fields (appliances, sinks, images).
 * Keeps a hidden input in sync for normal form POST / save.
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

    function markFormDirty(hidden) {
        const form = hidden.closest("form");
        if (!form) {
            return;
        }
        form.classList.add("form-dirty");
        const saveBtn = form.querySelector(".button-save");
        if (saveBtn) {
            saveBtn.removeAttribute("disabled");
        }
        hidden.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function syncHidden(hidden, data) {
        hidden.value = JSON.stringify(data);
        markFormDirty(hidden);
        hidden.dispatchEvent(
            new CustomEvent("card-payload-updated", { bubbles: true, detail: { name: hidden.name } })
        );
    }

    function readSinksOrdered(form) {
        const sinksHidden = form?.querySelector('input[type="hidden"][name="SinksJson"]');
        if (!sinksHidden) {
            return [];
        }
        const items = parseJson(sinksHidden.value, []);
        if (!Array.isArray(items)) {
            return [];
        }
        return items.filter((item) => (item?.value ?? "").trim().length > 0);
    }

    /* ---------- Key / value option lists (appliances, sinks) ---------- */

    function renderKeyValueRow(tbody, item, readOnly) {
        const tr = document.createElement("tr");
        tr.className = "card-kv-row";

        const labelCell = document.createElement("td");
        const labelInput = document.createElement("input");
        labelInput.type = "text";
        labelInput.className = "form-control form-control-sm";
        labelInput.placeholder = "Display label";
        labelInput.value = item?.value ?? "";
        labelInput.dataset.role = "value";
        if (readOnly) {
            labelInput.readOnly = true;
        }
        labelCell.appendChild(labelInput);

        const keyCell = document.createElement("td");
        keyCell.className = "card-kv-key-cell";
        const keyInput = document.createElement("input");
        keyInput.type = "number";
        keyInput.className = "form-control form-control-sm";
        keyInput.step = "1";
        keyInput.min = "0";
        keyInput.value = item?.key ?? 0;
        keyInput.dataset.role = "key";
        if (readOnly) {
            keyInput.readOnly = true;
        }
        keyCell.appendChild(keyInput);

        const actionsCell = document.createElement("td");
        actionsCell.className = "text-end text-nowrap";
        if (!readOnly) {
            const upBtn = document.createElement("button");
            upBtn.type = "button";
            upBtn.className = "btn btn-beta btn-sm me-1";
            upBtn.title = "Move up";
            upBtn.textContent = "↑";
            upBtn.dataset.action = "up";

            const downBtn = document.createElement("button");
            downBtn.type = "button";
            downBtn.className = "btn btn-beta btn-sm me-1";
            downBtn.title = "Move down";
            downBtn.textContent = "↓";
            downBtn.dataset.action = "down";

            const removeBtn = document.createElement("button");
            removeBtn.type = "button";
            removeBtn.className = "btn btn-beta btn-sm";
            removeBtn.title = "Remove";
            removeBtn.textContent = "Remove";
            removeBtn.dataset.action = "remove";

            actionsCell.append(upBtn, downBtn, removeBtn);
        }

        tr.append(labelCell, keyCell, actionsCell);
        tbody.appendChild(tr);
    }

    function collectKeyValueRows(tbody) {
        return Array.from(tbody.querySelectorAll(".card-kv-row"))
            .map((row) => ({
                value: (row.querySelector('[data-role="value"]')?.value ?? "").trim(),
                key: parseInt(row.querySelector('[data-role="key"]')?.value ?? "0", 10) || 0,
            }))
            .filter((row) => row.value.length > 0);
    }

    function findDuplicateKeys(rows) {
        const seen = new Map();
        const duplicates = new Set();
        for (const row of rows) {
            if (seen.has(row.key)) {
                duplicates.add(row.key);
            } else {
                seen.set(row.key, true);
            }
        }
        return [...duplicates].sort((a, b) => a - b);
    }

    function updateDuplicateKeyWarning(root, rows) {
        const alert = root.querySelector("[data-kv-duplicate-keys]");
        if (!alert) {
            return;
        }
        const duplicates = findDuplicateKeys(rows);
        if (duplicates.length === 0) {
            alert.classList.add("d-none");
            alert.textContent = "";
            return;
        }
        alert.classList.remove("d-none");
        alert.textContent = `Duplicate keys: ${duplicates.join(", ")}. Each option should have a unique key (used by price tables).`;
    }

    function initKeyValueEditor(root) {
        const hidden = root.querySelector('input[type="hidden"][data-card-json]');
        const tbody = root.querySelector("[data-kv-rows]");
        const addBtn = root.querySelector("[data-kv-add]");
        const readOnly = root.dataset.readonly === "true";

        if (!hidden || !tbody) {
            return;
        }

        const commit = () => {
            const rows = collectKeyValueRows(tbody);
            updateDuplicateKeyWarning(root, rows);
            syncHidden(hidden, rows);
        };

        const load = () => {
            tbody.replaceChildren();
            const parseError = root.querySelector("[data-kv-parse-error]");
            const raw = (hidden.value ?? "").trim();
            if (raw && raw !== "[]") {
                try {
                    const parsed = JSON.parse(raw);
                    if (!Array.isArray(parsed)) {
                        throw new Error("not array");
                    }
                } catch {
                    parseError?.classList.remove("d-none");
                    return;
                }
            }
            parseError?.classList.add("d-none");
            const items = parseJson(hidden.value, []);
            const list = Array.isArray(items) ? items : [];
            list.forEach((item) => renderKeyValueRow(tbody, item, readOnly));
            updateDuplicateKeyWarning(root, list);
        };

        load();

        root.addEventListener("input", (e) => {
            if (e.target.matches("[data-role='value'], [data-role='key']")) {
                commit();
            }
        });

        root.addEventListener("click", (e) => {
            const btn = e.target.closest("button[data-action]");
            if (!btn || readOnly) {
                return;
            }
            const row = btn.closest(".card-kv-row");
            if (!row) {
                return;
            }
            const rows = Array.from(tbody.querySelectorAll(".card-kv-row"));
            const index = rows.indexOf(row);

            if (btn.dataset.action === "remove") {
                row.remove();
                commit();
                return;
            }

            if (btn.dataset.action === "up" && index > 0) {
                tbody.insertBefore(row, rows[index - 1]);
                commit();
                return;
            }

            if (btn.dataset.action === "down" && index < rows.length - 1) {
                tbody.insertBefore(rows[index + 1], row);
                commit();
            }
        });

        addBtn?.addEventListener("click", () => {
            const rows = collectKeyValueRows(tbody);
            const nextKey = rows.reduce((max, r) => Math.max(max, r.key), -1) + 1;
            renderKeyValueRow(tbody, { value: "", key: nextKey }, readOnly);
            commit();
            tbody.lastElementChild?.querySelector('[data-role="value"]')?.focus();
        });
    }

    /* ---------- One image path per sink (flat array) ---------- */

    function flattenImagesPayload(data) {
        if (!Array.isArray(data) || data.length === 0) {
            return [];
        }
        if (typeof data[0] === "string") {
            return data.map((p) => (p ?? "").trim());
        }
        const flat = [];
        for (const item of data) {
            if (!Array.isArray(item)) {
                continue;
            }
            for (const path of item) {
                if (typeof path === "string") {
                    flat.push(path.trim());
                }
            }
        }
        return flat;
    }

    function renderSinkImageRow(tbody, sink, path, readOnly) {
        const tr = document.createElement("tr");
        tr.className = "card-img-row";
        tr.dataset.sinkKey = String(sink?.key ?? 0);

        const labelCell = document.createElement("td");
        labelCell.className = "text-nowrap";
        const key = sink?.key ?? 0;
        const label = sink?.value ?? "";
        labelCell.innerHTML = `<span class="text-muted small me-1">#${key}</span> ${escapeHtml(label)}`;

        const pathCell = document.createElement("td");
        const input = document.createElement("input");
        input.type = "text";
        input.className = "form-control form-control-sm font-monospace";
        input.placeholder = "Cloudinary path";
        input.value = path ?? "";
        input.dataset.role = "path";
        if (readOnly) {
            input.readOnly = true;
        }
        pathCell.appendChild(input);

        tr.append(labelCell, pathCell);
        tbody.appendChild(tr);
    }

    function escapeHtml(text) {
        const div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    function collectSinkImagePaths(tbody) {
        return Array.from(tbody.querySelectorAll(".card-img-row")).map((row) =>
            (row.querySelector('[data-role="path"]')?.value ?? "").trim()
        );
    }

    function buildRowsFromSinks(tbody, sinks, existingPaths, readOnly) {
        tbody.replaceChildren();
        sinks.forEach((sink, index) => {
            const path = index < existingPaths.length ? existingPaths[index] : "";
            renderSinkImageRow(tbody, sink, path, readOnly);
        });
    }

    function initImagesEditor(root) {
        const hidden = root.querySelector('input[type="hidden"][data-card-json]');
        const tbody = root.querySelector("[data-img-rows]");
        const syncBtn = root.querySelector("[data-img-sync-sinks]");
        const noSinksAlert = root.querySelector("[data-img-no-sinks]");
        const readOnly = root.dataset.readonly === "true";
        const form = root.closest("form");

        if (!hidden || !tbody) {
            return;
        }

        const commit = () => {
            const paths = collectSinkImagePaths(tbody);
            syncHidden(hidden, paths);
        };

        const rebuild = (preservePaths = true) => {
            const parseError = root.querySelector("[data-img-parse-error]");
            parseError?.classList.add("d-none");

            const sinks = readSinksOrdered(form);
            if (sinks.length === 0) {
                tbody.replaceChildren();
                noSinksAlert?.classList.remove("d-none");
                syncHidden(hidden, []);
                return;
            }

            noSinksAlert?.classList.add("d-none");

            let existingPaths = [];
            if (preservePaths) {
                const raw = (hidden.value ?? "").trim();
                if (raw && raw !== "[]") {
                    try {
                        const parsed = JSON.parse(raw);
                        if (!Array.isArray(parsed)) {
                            throw new Error("not array");
                        }
                        existingPaths = flattenImagesPayload(parsed);
                    } catch {
                        parseError?.classList.remove("d-none");
                        return;
                    }
                } else {
                    existingPaths = collectSinkImagePaths(tbody);
                }
            }

            buildRowsFromSinks(tbody, sinks, existingPaths, readOnly);
            commit();
        };

        rebuild(true);

        tbody.addEventListener("input", (e) => {
            if (e.target.matches('[data-role="path"]')) {
                commit();
            }
        });

        syncBtn?.addEventListener("click", () => rebuild(true));

        const onSinksUpdated = (e) => {
            if (e.detail?.name === "SinksJson" || e.target?.name === "SinksJson") {
                rebuild(true);
            }
        };
        form?.addEventListener("card-payload-updated", onSinksUpdated);
        form?.addEventListener("change", (e) => {
            if (e.target?.name === "SinksJson") {
                rebuild(true);
            }
        });
    }

    function flushEditors() {
        document.querySelectorAll("[data-card-kv-editor]").forEach((root) => {
            const hidden = root.querySelector('input[type="hidden"][data-card-json]');
            const tbody = root.querySelector("[data-kv-rows]");
            if (!hidden || !tbody) {
                return;
            }
            const rows = collectKeyValueRows(tbody);
            updateDuplicateKeyWarning(root, rows);
            hidden.value = JSON.stringify(rows);
        });

        document.querySelectorAll("[data-card-images-editor]").forEach((root) => {
            const hidden = root.querySelector('input[type="hidden"][data-card-json]');
            const tbody = root.querySelector("[data-img-rows]");
            if (!hidden || !tbody) {
                return;
            }
            hidden.value = JSON.stringify(collectSinkImagePaths(tbody));
        });
    }

    function initAll() {
        document.querySelectorAll("[data-card-kv-editor]").forEach(initKeyValueEditor);
        document.querySelectorAll("[data-card-images-editor]").forEach(initImagesEditor);

        document.querySelectorAll("form.form-edit, #new-form").forEach((form) => {
            form.addEventListener("submit", flushEditors, true);
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAll);
    } else {
        initAll();
    }
})();
