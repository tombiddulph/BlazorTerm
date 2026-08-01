const target = document.getElementById("activity-map");

if (target) {
    initializeMap(target);
}

async function initializeMap(container) {
    const status = document.getElementById("activity-map-status");
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    try {
        const [maplibreModule, response] = await Promise.all([
            import("https://cdn.jsdelivr.net/npm/maplibre-gl@6.1.0/+esm"),
            fetch("/activity-map.json")
        ]);
        const maplibregl = maplibreModule.default ?? maplibreModule;

        if (!response.ok) {
            throw new Error(`Activity data returned ${response.status}`);
        }

        const data = await response.json();
        const map = new maplibregl.Map({
            container,
            center: [-28, 46],
            zoom: 1.35,
            minZoom: 1.2,
            projection: { type: "globe" },
            style: {
                version: 8,
                glyphs: "https://fonts.openmaptiles.org/{fontstack}/{range}.pbf",
                sources: {
                    carto: {
                        type: "raster",
                        tiles: [
                            "https://a.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png",
                            "https://b.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png",
                            "https://c.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png"
                        ],
                        tileSize: 512,
                        attribution: "&copy; OpenStreetMap contributors &copy; CARTO"
                    }
                },
                layers: [
                    { id: "space", type: "background", paint: { "background-color": "#030405" } },
                    {
                        id: "basemap",
                        type: "raster",
                        source: "carto",
                        paint: {
                            "raster-brightness-max": 0.78,
                            "raster-contrast": 0.08,
                            "raster-opacity": 0.82,
                            "raster-saturation": -0.7
                        }
                    }
                ]
            }
        });

        map.addControl(new maplibregl.NavigationControl({ showCompass: false }), "top-right");
        map.once("load", () => renderRoutes(map, data, reducedMotion));
        document.getElementById("map-route-detail").textContent = `${data.geometryPrecisionMeters} m`;
        renderSportBreakdown(data);
    } catch (error) {
        console.error(error);
        status.textContent = "The activity map could not be loaded.";
    }
}

function renderRoutes(map, data, reducedMotion) {
    const initialFilter = ["<=", ["get", "reveal"], reducedMotion ? 1 : -1];
    data.features.forEach(feature => {
        feature.properties.reveal = revealPosition(feature.geometry.coordinates);
    });

    map.addSource("activity-routes", { type: "geojson", data });
    map.addLayer({
        id: "activity-core",
        type: "line",
        source: "activity-routes",
        filter: initialFilter,
        layout: { "line-cap": "round", "line-join": "round" },
        paint: {
            "line-color": "#ff5a36",
            "line-opacity": 0.88,
            "line-width": ["interpolate", ["linear"], ["zoom"], 2, 0.65, 8, 1.4, 13, 2.5]
        }
    });

    map.fitBounds([[-9.5, 49.3], [2.5, 61.1]], {
        padding: window.innerWidth < 800 ? 24 : 60,
        duration: reducedMotion ? 0 : 3_600
    });

    document.getElementById("activity-map-status").hidden = true;
    if (reducedMotion) {
        updateCounters(data, 1);
        return;
    }

    const replay = document.getElementById("activity-map-replay");
    const play = () => animateTrace(map, data, replay);
    replay.addEventListener("click", play);
    window.setTimeout(play, 650);
}

function animateTrace(map, data, replay) {
    const startedAt = performance.now();
    const duration = 3_200;
    let lastStep = -1;

    replay.disabled = true;
    replay.hidden = false;
    replay.textContent = "Tracing...";
    map.setFilter("activity-core", ["<=", ["get", "reveal"], -1]);
    updateCounters(data, 0);

    const frame = now => {
        const progress = Math.min(1, (now - startedAt) / duration);
        const step = Math.floor(progress * 100);

        if (step !== lastStep) {
            const threshold = step / 100;
            const filter = ["<=", ["get", "reveal"], threshold];
            map.setFilter("activity-core", filter);
            updateCounters(data, threshold);
            lastStep = step;
        }

        if (progress < 1) {
            requestAnimationFrame(frame);
            return;
        }

        updateCounters(data, 1);
        replay.disabled = false;
        replay.textContent = "Replay trace";
    };

    requestAnimationFrame(frame);
}

function updateCounters(data, progress) {
    const activities = Math.round(data.activityCount * progress);
    const distance = Math.round(data.distanceKilometers * progress);
    const miles = Math.round(distance * 0.621371);
    const elevation = Math.round(data.elevationMeters * progress);
    const elevationFeet = Math.round(elevation * 3.28084);
    document.getElementById("map-activity-count").textContent = activities.toLocaleString();
    document.getElementById("map-distance-count").textContent = `${distance.toLocaleString()} km / ${miles.toLocaleString()} mi`;
    document.getElementById("map-elevation-count").textContent = `${elevation.toLocaleString()} m / ${elevationFeet.toLocaleString()} ft`;
}

function renderSportBreakdown(data) {
    const list = document.getElementById("map-sport-breakdown");
    const maximum = Math.max(...data.sports.map(sport => sport.count));
    list.replaceChildren(...data.sports.map(sport => {
        const item = document.createElement("li");
        const name = document.createElement("span");
        const count = document.createElement("strong");
        name.textContent = sport.name.replace(/([a-z])([A-Z])/g, "$1 $2");
        count.textContent = sport.count.toLocaleString();
        item.style.setProperty("--sport-share", `${sport.count / maximum * 100}%`);
        item.append(name, count);
        return item;
    }));
}

function revealPosition(coordinates) {
    const first = coordinates[0];
    const middle = coordinates[Math.floor(coordinates.length / 2)];
    const last = coordinates[coordinates.length - 1];
    const value = Math.sin(
        first[0] * 12.9898 + first[1] * 78.233
        + middle[0] * 37.719 + middle[1] * 19.913
        + last[0] * 4.141 + last[1] * 53.731
    ) * 43_758.5453;
    return value - Math.floor(value);
}
