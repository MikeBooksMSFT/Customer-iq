let customerIndex = [];
let customerData = {};

function lineList(items) {
  return Array.isArray(items) && items.length ? items : ["No data available."];
}

function sectionLines(payload, sectionName) {
  const sections = payload.sections || {};
  return lineList(sections[sectionName]);
}

function iqText(payload) {
  return payload.iq_markdown || "No Customer IQ output available.";
}

function formatMsxSnapshot(snapshot) {
  if (!snapshot || snapshot.status === "missing") {
    return snapshot?.message || "No MSX snapshot available.";
  }

  const lines = [
    `Customer: ${snapshot.customerName || snapshot.customer || "Unknown"}`,
    `Source: ${snapshot.source || "MSX snapshot"}`,
    `Status: ${snapshot.status || "available"}`,
    `Captured: ${snapshot.capturedAt || "unknown"}`,
    `Grounding: ${snapshot.grounding || "not specified"}`,
    "",
  ];

  (snapshot.notes || []).forEach((note) => lines.push(`- Note: ${note}`));
  if (snapshot.notes && snapshot.notes.length) {
    lines.push("");
  }

  (snapshot.opportunities || []).forEach((item, index) => {
    lines.push(`${index + 1}. ${item.name}`);
    lines.push(`   Value: ${item.value || "N/A"} | Close: ${item.closeDate || "N/A"} | Stage: ${item.stage || "N/A"}`);
    lines.push(`   Solution Area: ${item.solutionArea || "N/A"} | Sales Play: ${item.salesPlay || "N/A"}`);
    lines.push(`   Owner: ${item.owner || "N/A"} | MSX ID: ${item.msxId || "N/A"}`);
    if (item.opportunityId) {
      lines.push(`   Opportunity ID: ${item.opportunityId}`);
    }
  });

  return lines.join("\n").trim();
}

function renderList(elementId, items) {
  const root = document.getElementById(elementId);
  root.innerHTML = "";
  lineList(items).forEach((item) => {
    const li = document.createElement("li");
    li.textContent = item;
    root.appendChild(li);
  });
}

function answerQuestion(payload, question) {
  const prompt = question.toLowerCase();
  let topic = "Current State";

  if (prompt.includes("risk")) {
    topic = "Risks";
  } else if (prompt.includes("opportun") || prompt.includes("play")) {
    topic = "Opportunities";
  } else if (prompt.includes("action") || prompt.includes("next")) {
    topic = "Next Actions";
  } else if (prompt.includes("signal") || prompt.includes("meeting")) {
    topic = "Signals";
  }

  const responseLines = sectionLines(payload, topic);
  const evidenceLines = lineList(payload.context_sections?.Signals || []).slice(0, 3);

  return [
    `${topic} for ${payload.name}:`,
    "",
    ...responseLines.map((line) => `- ${line}`),
    "",
    "Supporting evidence:",
    "",
    ...evidenceLines.map((line) => `- ${line}`),
  ].join("\n");
}

async function fetchJson(path) {
  const response = await fetch(path);
  if (!response.ok) {
    throw new Error(`Failed to load ${path}`);
  }
  return response.json();
}

async function loadIndex() {
  customerIndex = await fetchJson("./data/customers.json");
  const select = document.getElementById("customer");
  select.innerHTML = "";
  customerIndex.forEach((customer) => {
    const option = document.createElement("option");
    option.value = customer.slug;
    option.textContent = customer.name;
    select.appendChild(option);
  });
}

async function loadCustomer(slug) {
  if (!customerData[slug]) {
    customerData[slug] = await fetchJson(`./data/${slug}.json`);
  }
  const payload = customerData[slug];
  document.getElementById("iq-output").textContent = iqText(payload);
  document.getElementById("msx-output").textContent = formatMsxSnapshot(payload.msx_snapshot);
  renderList("metadata-list", payload.context_sections?.Metadata || []);
  renderList("connections-list", payload.context_sections?.["System Connections"] || []);
  renderList("signals-list", payload.context_sections?.Signals || []);
  renderList("change-log-list", payload.context_sections?.["Change Log"] || []);
  document.getElementById("agent-output").textContent = "Ask about risks, opportunities, next actions, or current state.";
}

async function boot() {
  await loadIndex();
  const initial = document.getElementById("customer").value;
  await loadCustomer(initial);

  document.getElementById("load-iq").addEventListener("click", async () => {
    await loadCustomer(document.getElementById("customer").value);
  });

  document.getElementById("ask-agent").addEventListener("click", async () => {
    const slug = document.getElementById("customer").value;
    const question = document.getElementById("question").value.trim();
    if (!question) {
      document.getElementById("agent-output").textContent = "Enter a question first.";
      return;
    }
    const payload = customerData[slug] || await fetchJson(`./data/${slug}.json`);
    customerData[slug] = payload;
    document.getElementById("agent-output").textContent = answerQuestion(payload, question);
  });
}

boot().catch((error) => {
  document.getElementById("iq-output").textContent = `Failed to load site data: ${error.message}`;
  document.getElementById("msx-output").textContent = `Failed to load site data: ${error.message}`;
});
