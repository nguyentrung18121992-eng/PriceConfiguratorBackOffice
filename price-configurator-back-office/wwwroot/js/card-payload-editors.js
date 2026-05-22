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

    function readSinksLabels(form) {
        const sinksHidden = form?.querySelector('input[type="hidden"][name="SinksJson"]');
        if (!sinksHidden) {
            return [];
        }
        const items = parseJson(sinksHidden.value, []);
        if (!Array.isArray(items)) {
            return [];
        }
        return items
            .slice()
            .sort((a, b) => (a.key ?? 0) - (b.key ?? 0))
            .map((item) => item.value ?? "");
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

    /* ---------- Nested image path sets ---------- */

    function renderPathRow(pathsContainer, path, readOnly) {
        const row = document.createElement("div");
        row.className = "input-group input-group-sm mb-1 card-img-path-row";

        const input = document.createElement("input");
        input.type = "text";
        input.className = "form-control";
        input.placeholder = "Cloudinary path (e.g. Magnet/Price Configurator/…)";
        input.value = path ?? "";
        input.dataset.role = "path";
        if (readOnly) {
            input.readOnly = true;
        }

        row.appendChild(input);

        if (!readOnly) {
            const removeBtn = document.createElement("button");
            removeBtn.type = "button";
            removeBtn.className = "btn btn-beta";
            removeBtn.textContent = "Remove";
            removeBtn.dataset.action = "remove-path";
            row.appendChild(removeBtn);
        }

        pathsContainer.appendChild(row);
    }

    function renderImageSet(setsContainer, paths, label, readOnly) {
        const card = document.createElement("div");
        card.className = "card mb-2 card-img-set";
        card.dataset.set = "true";

        const header = document.createElement("div");
        header.className =
            "card-header py-2 d-flex justify-content-between align-items-center";
        const title = document.createElement("span");
        title.className = "fw-semibold small";
        title.dataset.role = "set-title";
        title.textContent = label;
        header.appendChild(title);

        if (!readOnly) {
            const removeSet = document.createElement("button");
            removeSet.type = "button";
            removeSet.className = "btn btn-beta btn-sm";
            removeSet.textContent = "Remove set";
            removeSet.dataset.action = "remove-set";
            header.appendChild(removeSet);
        }

        const body = document.createElement("div");
        body.className = "card-body py-2";
        const pathsContainer = document.createElement("div");
        pathsContainer.dataset.role = "paths";

        const list = Array.isArray(paths) && paths.length > 0 ? paths : [""];
        list.forEach((p) => renderPathRow(pathsContainer, p, readOnly));

        if (!readOnly) {
            const addPath = document.createElement("button");
            addPath.type = "button";
            addPath.className = "btn btn-beta btn-sm mt-1";
            addPath.textContent = "+ Add path";
            addPath.dataset.action = "add-path";
            body.append(pathsContainer, addPath);
        } else {
            body.appendChild(pathsContainer);
        }

        card.append(header, body);
        setsContainer.appendChild(card);
    }

    function collectImageSets(setsContainer) {
        return Array.from(setsContainer.querySelectorAll(".card-img-set")).map((set) =>
            Array.from(set.querySelectorAll('[data-role="path"]'))
                .map((input) => input.value.trim())
                .filter((p) => p.length > 0)
        );
    }

    function refreshImageSetTitles(setsContainer, sinkLabels) {
        Array.from(setsContainer.querySelectorAll(".card-img-set")).forEach((set, index) => {
            const title = set.querySelector('[data-role="set-title"]');
            if (!title) {
                return;
            }
            const sinkLabel = sinkLabels[index];
            title.textContent = sinkLabel
                ? `Sink option ${index + 1}: ${sinkLabel}`
                : `Image set ${index + 1}`;
        });
    }

    function initImagesEditor(root) {
        const hidden = root.querySelector('input[type="hidden"][data-card-json]');
        const setsContainer = root.querySelector("[data-img-sets]");
        const addSetBtn = root.querySelector("[data-img-add-set]");
        const alignBtn = root.querySelector("[data-img-align-sinks]");
        const readOnly = root.dataset.readonly === "true";
        const form = root.closest("form");

        if (!hidden || !setsContainer) {
            return;
        }

        const commit = () => {
            const sets = collectImageSets(setsContainer).filter((paths) => paths.length > 0);
            syncHidden(hidden, sets);
        };

        const load = () => {
            setsContainer.replaceChildren();
            const parseError = root.querySelector("[data-img-parse-error]");
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

            const data = parseJson(hidden.value, []);
            const sets = Array.isArray(data) ? data : [];
            const sinkLabels = readSinksLabels(form);

            if (sets.length > 0) {
                sets.forEach((paths, index) => {
                    const label = sinkLabels[index]
                        ? `Sink option ${index + 1}: ${sinkLabels[index]}`
                        : `Image set ${index + 1}`;
                    renderImageSet(setsContainer, paths, label, readOnly);
                });
            }
        };

        load();

        const onSinksUpdated = (e) => {
            if (e.detail?.name === "SinksJson" || e.target?.name === "SinksJson") {
                refreshImageSetTitles(setsContainer, readSinksLabels(form));
            }
        };
        form?.addEventListener("card-payload-updated", onSinksUpdated);
        form?.addEventListener("change", (e) => {
            if (e.target?.name === "SinksJson") {
                refreshImageSetTitles(setsContainer, readSinksLabels(form));
            }
        });

        setsContainer.addEventListener("input", (e) => {
            if (e.target.matches('[data-role="path"]')) {
                commit();
            }
        });

        setsContainer.addEventListener("click", (e) => {
            const btn = e.target.closest("button[data-action]");
            if (!btn || readOnly) {
                return;
            }

            if (btn.dataset.action === "add-path") {
                const set = btn.closest(".card-img-set");
                const pathsContainer = set?.querySelector('[data-role="paths"]');
                if (pathsContainer) {
                    renderPathRow(pathsContainer, "", readOnly);
                    commit();
                    pathsContainer.lastElementChild?.querySelector("input")?.focus();
                }
                return;
            }

            if (btn.dataset.action === "remove-path") {
                const row = btn.closest(".card-img-path-row");
                const set = btn.closest(".card-img-set");
                const pathsContainer = set?.querySelector('[data-role="paths"]');
                row?.remove();
                if (pathsContainer && pathsContainer.children.length === 0) {
                    renderPathRow(pathsContainer, "", readOnly);
                }
                commit();
                return;
            }

            if (btn.dataset.action === "remove-set") {
                btn.closest(".card-img-set")?.remove();
                commit();
                refreshImageSetTitles(setsContainer, readSinksLabels(form));
            }
        });

        addSetBtn?.addEventListener("click", () => {
            const index = setsContainer.querySelectorAll(".card-img-set").length;
            const sinkLabels = readSinksLabels(form);
            const label = sinkLabels[index]
                ? `Sink option ${index + 1}: ${sinkLabels[index]}`
                : `Image set ${index + 1}`;
            renderImageSet(setsContainer, [""], label, readOnly);
            commit();
        });

        alignBtn?.addEventListener("click", () => {
            const sinkLabels = readSinksLabels(form);
            if (sinkLabels.length === 0) {
                return;
            }
            const existing = collectImageSets(setsContainer);
            setsContainer.replaceChildren();
            sinkLabels.forEach((sinkLabel, index) => {
                const paths = existing[index] ?? [""];
                renderImageSet(
                    setsContainer,
                    paths.length > 0 ? paths : [""],
                    `Sink option ${index + 1}: ${sinkLabel}`,
                    readOnly
                );
            });
            commit();
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
            const setsContainer = root.querySelector("[data-img-sets]");
            if (!hidden || !setsContainer) {
                return;
            }
            const sets = collectImageSets(setsContainer).filter((paths) => paths.length > 0);
            hidden.value = JSON.stringify(sets);
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
