const modal = document.getElementById("components-reconnect-modal");
const retryButton = document.getElementById("components-reconnect-button");
const resumeButton = document.getElementById("components-resume-button");

modal.addEventListener("components-reconnect-state-changed", handleStateChange);
retryButton.addEventListener("click", retry);
resumeButton.addEventListener("click", resume);

function handleStateChange(event) {
    if (event.detail.state === "show") {
        if (!modal.open) modal.showModal();
    } else if (event.detail.state === "hide") {
        if (modal.open) modal.close();
    } else if (event.detail.state === "failed") {
        document.addEventListener("visibilitychange", retryWhenVisible);
    } else if (event.detail.state === "rejected") {
        location.reload();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenVisible);

    try {
        if (!await Blazor.reconnect() && !await Blazor.resumeCircuit()) {
            location.reload();
        }
    } catch {
        document.addEventListener("visibilitychange", retryWhenVisible);
    }
}

async function resume() {
    try {
        if (!await Blazor.resumeCircuit()) location.reload();
    } catch {
        modal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}

async function retryWhenVisible() {
    if (document.visibilityState === "visible") await retry();
}
