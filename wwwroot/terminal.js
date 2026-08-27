// Firefox does not support `field-sizing: content`, which the block cursor
// relies on to sit at the end of the typed text. Mirror the value length into
// an explicit ch width there instead.
const needsWidthFallback = !(window.CSS && CSS.supports("field-sizing", "content"));

function syncInputWidth(input) {
    if (!needsWidthFallback || !input) return;
    input.style.width = Math.max(1, input.value.length) + "ch";
}

window.terminalUi = {
    initializeTheme: function (currentTheme, preferCurrent) {
        const validThemes = ["theme-green", "theme-amber", "theme-nord", "theme-solarized", "theme-dracula"];
        let selected = currentTheme;
        try {
            const stored = localStorage.getItem("blazorterm-theme");
            if (!preferCurrent && validThemes.includes(stored)) selected = stored;
            localStorage.setItem("blazorterm-theme", selected);
        } catch { }
        document.documentElement.dataset.terminalTheme = selected;
        return selected;
    },
    storeTheme: function (theme) {
        try { localStorage.setItem("blazorterm-theme", theme); } catch { }
        document.documentElement.dataset.terminalTheme = theme;
    },
    clear: function (inputId) {
        const input = document.getElementById(inputId);
        if (input) {
            input.value = "";
            syncInputWidth(input);
        }
    },
    focus: function (inputId) {
        const input = document.getElementById(inputId);
        if (input) input.focus({ preventScroll: true });
    },
    focusAndScroll: function (inputId, outputId) {
        const input = document.getElementById(inputId);
        const output = document.getElementById(outputId);
        if (input) {
            input.focus({ preventScroll: true });
        }
        if (output) output.scrollTop = output.scrollHeight;
    }
};

const blazorError = document.getElementById("blazor-error-ui");
const reloadButton = blazorError?.querySelector(".reload");
const dismissButton = blazorError?.querySelector(".dismiss");
reloadButton?.addEventListener("click", () => location.reload());
dismissButton?.addEventListener("click", () => blazorError.style.display = "none");

function startTerminalBoot() {
    const overlay = document.getElementById("terminal-boot");
    const windowElement = overlay?.closest(".terminal-window");
    const root = document.documentElement;
    if (!overlay || !windowElement || !root.classList.contains("terminal-boot-pending")) return;

    let finished = false;
    const motionTimers = [];
    windowElement.classList.add("booting");
    root.classList.add("terminal-boot-running");

    const finishBoot = () => {
        if (finished) return;
        finished = true;
        motionTimers.forEach(clearTimeout);
        observer.disconnect();
        overlay.removeEventListener("animationend", handleAnimationEnd);
        document.removeEventListener("pointerdown", finishBoot);
        document.removeEventListener("keydown", finishBoot);
        root.classList.remove("terminal-boot-pending");
        root.classList.remove("terminal-boot-running");
        windowElement.classList.remove("booting");
    };

    const handleAnimationEnd = event => {
        if (event.target === overlay && event.animationName === "boot-overlay-exit") finishBoot();
    };

    const observer = new MutationObserver(() => {
        if (!root.classList.contains("terminal-boot-pending")) finishBoot();
    });
    observer.observe(root, { attributes: true, attributeFilter: ["class"] });
    overlay.addEventListener("animationend", handleAnimationEnd);
    document.addEventListener("pointerdown", finishBoot);
    document.addEventListener("keydown", finishBoot);

    requestAnimationFrame(() => {
        if (finished) return;
        overlay.querySelectorAll("animateMotion").forEach(motion => {
            const timer = setTimeout(() => motion.beginElement(), Number(motion.dataset.delay));
            motionTimers.push(timer);
        });
    });
}

startTerminalBoot();

document.addEventListener("keydown", function (event) {
    if (!event.target || event.target.id !== "terminal-input") return;

    if (event.key === "Tab") {
        event.preventDefault();
        if (event.target.dataset.vimMode !== "true") completeInput(event.target);
        syncInputWidth(event.target);
        event.stopImmediatePropagation();
        return;
    }

    if (event.key === "Enter") {
        event.target.dispatchEvent(new Event("change", { bubbles: true }));
        event.target.value = "";
        syncInputWidth(event.target);
        return;
    }

    if (event.key === "ArrowUp" || event.key === "ArrowDown" ||
        (event.ctrlKey && event.key.toLowerCase() === "l")) {
        event.preventDefault();
        // History recall replaces the value after a server round trip, which
        // fires no input event; re-sync shortly after.
        if (needsWidthFallback) {
            const input = event.target;
            [50, 150, 400].forEach(delay => setTimeout(() => syncInputWidth(input), delay));
        }
        return;
    }

    // Blazor handles commands, not individual characters. Keeping ordinary
    // key events local prevents delayed server renders from replacing newer input.
    event.stopImmediatePropagation();
});

document.addEventListener("input", function (event) {
    if (event.target && event.target.id === "terminal-input")
        syncInputWidth(event.target);
});

// Mobile browsers only open the software keyboard when focus occurs directly
// inside the user's gesture, not after a Blazor Server round trip.
document.addEventListener("pointerdown", function (event) {
    if (!event.target.closest(".terminal-window") ||
        event.target.closest("a, button, input, dialog")) {
        return;
    }

    const input = document.getElementById("terminal-input");
    if (input) input.focus({ preventScroll: true });
}, true);

function completeInput(input) {
    const value = input.value;
    const pipeIndex = findLastPipeline(value);
    const segment = value.substring(pipeIndex + 1);
    const trimmedSegment = segment.trimStart();
    const leadingWhitespace = segment.length - trimmedSegment.length;
    const lastSpace = Math.max(trimmedSegment.lastIndexOf(" "), trimmedSegment.lastIndexOf("\t"));
    const prefix = trimmedSegment.substring(lastSpace + 1);
    const command = trimmedSegment.split(/\s+/)[0].toLowerCase();
    const projectCommand = command === "project" || command === "open";
    const pathCommand = command === "cat" || command === "ls" || command === "tree";
    let dataName;
    if (lastSpace === -1) {
        dataName = pipeIndex === -1 ? "commandCompletions" : "filterCompletions";
    } else if (projectCommand) {
        dataName = "projectCompletions";
    } else if (command === "cd") {
        dataName = "directoryCompletions";
    } else if (pathCommand) {
        dataName = "pathCompletions";
    } else if (command === "trace") {
        dataName = "commandCompletions";
    } else if (command === "help") {
        dataName = "helpCompletions";
    } else if (command === "man") {
        dataName = "manCompletions";
    } else if (command === "tour") {
        dataName = "tourCompletions";
    }
    const options = (input.dataset[dataName] || "").split(",").filter(Boolean);
    const matches = options.filter(option => option.toLowerCase().startsWith(prefix.toLowerCase()));

    if (matches.length === 0) return;

    let completion = matches[0];
    if (matches.length > 1) {
        completion = matches.reduce(function (common, match) {
            let length = 0;
            while (length < common.length && length < match.length &&
                   common[length].toLowerCase() === match[length].toLowerCase()) {
                length++;
            }
            return common.substring(0, length);
        });
    }

    const suffix = matches.length === 1 && !completion.endsWith("/") ? " " : "";
    const replacementStart = pipeIndex + leadingWhitespace + lastSpace + 2;
    input.value = value.substring(0, replacementStart) + completion + suffix;
}

function findLastPipeline(value) {
    let quote = null;
    let escaped = false;
    let lastPipe = -1;

    for (let index = 0; index < value.length; index++) {
        const character = value[index];
        if (escaped) {
            escaped = false;
        } else if (character === "\\") {
            escaped = true;
        } else if (quote) {
            if (character === quote) quote = null;
        } else if (character === "'" || character === '"') {
            quote = character;
        } else if (character === "|") {
            lastPipe = index;
        }
    }

    return lastPipe;
}

document.addEventListener("visibilitychange", async function () {
    if (!window.Blazor) return;

    if (document.visibilityState === "hidden" && Blazor.pauseCircuit) {
        await Blazor.pauseCircuit();
    } else if (document.visibilityState === "visible" && Blazor.resumeCircuit) {
        await Blazor.resumeCircuit();
    }
});
