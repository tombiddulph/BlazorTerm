window.terminalUi = {
    focus: function (inputId) {
        const input = document.getElementById(inputId);
        if (input) input.focus({ preventScroll: true });
    },
    focusAndScroll: function (inputId, outputId) {
        const input = document.getElementById(inputId);
        const output = document.getElementById(outputId);
        if (input) input.focus({ preventScroll: true });
        if (output) output.scrollTop = output.scrollHeight;
    }
};

const blazorError = document.getElementById("blazor-error-ui");
const reloadButton = blazorError?.querySelector(".reload");
const dismissButton = blazorError?.querySelector(".dismiss");
reloadButton?.addEventListener("click", () => location.reload());
dismissButton?.addEventListener("click", () => blazorError.style.display = "none");

document.addEventListener("keydown", function (event) {
    if (!event.target || event.target.id !== "terminal-input") return;

    if (event.key === "Tab") {
        event.preventDefault();
        completeInput(event.target);
        event.stopImmediatePropagation();
        return;
    }

    if (event.key === "Enter") {
        event.target.dispatchEvent(new Event("change", { bubbles: true }));
        return;
    }

    if (event.key === "ArrowUp" || event.key === "ArrowDown" ||
        (event.ctrlKey && event.key.toLowerCase() === "l")) {
        event.preventDefault();
        return;
    }

    // Blazor handles commands, not individual characters. Keeping ordinary
    // key events local prevents delayed server renders from replacing newer input.
    event.stopImmediatePropagation();
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
    const lastSpace = value.lastIndexOf(" ");
    const prefix = value.substring(lastSpace + 1);
    const command = value.trimStart().split(" ")[0].toLowerCase();
    const projectCommand = command === "project" || command === "open";
    const dataName = lastSpace === -1
        ? "commandCompletions"
        : projectCommand ? "projectCompletions" : "fileCompletions";
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

    const suffix = matches.length === 1 && lastSpace === -1 ? " " : "";
    input.value = value.substring(0, lastSpace + 1) + completion + suffix;
}

document.addEventListener("visibilitychange", async function () {
    if (!window.Blazor) return;

    if (document.visibilityState === "hidden" && Blazor.pauseCircuit) {
        await Blazor.pauseCircuit();
    } else if (document.visibilityState === "visible" && Blazor.resumeCircuit) {
        await Blazor.resumeCircuit();
    }
});
