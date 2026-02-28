const state = {
  token: localStorage.getItem("fet_token") || "",
  email: localStorage.getItem("fet_email") || "",
  tab: "dashboard",
  page: 1,
  pageSize: 20,
  category: ""
};

const els = {
  authCard: document.getElementById("auth-card"),
  appContent: document.getElementById("app-content"),
  authState: document.getElementById("auth-state"),
  logoutBtn: document.getElementById("logout-btn"),
  toast: document.getElementById("toast"),

  loginForm: document.getElementById("login-form"),
  registerForm: document.getElementById("register-form"),

  metricTotal: document.getElementById("m-total"),
  metricExpiring: document.getElementById("m-expiring"),
  metricUsed: document.getElementById("m-used"),
  metricWaste: document.getElementById("m-waste"),

  addProductForm: document.getElementById("add-product-form"),
  inventoryList: document.getElementById("inventory-list"),
  categoryFilter: document.getElementById("category-filter"),
  applyFilter: document.getElementById("apply-filter"),

  scanForm: document.getElementById("scan-form"),
  correctForm: document.getElementById("correct-form"),

  recipeList: document.getElementById("recipe-list"),
  refreshRecipes: document.getElementById("refresh-recipes")
};

function showToast(message, timeout = 2200) {
  els.toast.textContent = message;
  els.toast.classList.remove("hidden");
  window.setTimeout(() => els.toast.classList.add("hidden"), timeout);
}

async function api(path, options = {}) {
  const headers = {
    "Content-Type": "application/json",
    ...(options.headers || {})
  };

  if (state.token) {
    headers.Authorization = `Bearer ${state.token}`;
  }

  const response = await fetch(path, { ...options, headers });

  if (!response.ok) {
    const payload = await response.json().catch(() => ({ error: "Request failed" }));
    throw new Error(payload.error || `HTTP ${response.status}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

function setAuth(token, email) {
  state.token = token;
  state.email = email;
  localStorage.setItem("fet_token", token);
  localStorage.setItem("fet_email", email);
  syncAuthUi();
}

function clearAuth() {
  state.token = "";
  state.email = "";
  localStorage.removeItem("fet_token");
  localStorage.removeItem("fet_email");
  syncAuthUi();
}

function syncAuthUi() {
  const authed = Boolean(state.token);
  els.authCard.classList.toggle("hidden", authed);
  els.appContent.classList.toggle("hidden", !authed);
  els.logoutBtn.classList.toggle("hidden", !authed);
  els.authState.textContent = authed ? state.email : "Guest";

  if (authed) {
    refreshDashboard();
    refreshInventory();
    refreshRecipes();
  }
}

function switchAuthTab(mode) {
  document.querySelectorAll(".auth-tab").forEach((btn) => {
    btn.classList.toggle("active", btn.dataset.authTab === mode);
  });

  els.loginForm.classList.toggle("hidden", mode !== "login");
  els.registerForm.classList.toggle("hidden", mode !== "register");
}

function switchMainTab(tab) {
  state.tab = tab;
  document.querySelectorAll(".section-tab").forEach((btn) => {
    btn.classList.toggle("active", btn.dataset.tab === tab);
  });

  document.querySelectorAll(".tab-panel").forEach((panel) => {
    panel.classList.add("hidden");
  });

  document.getElementById(`tab-${tab}`).classList.remove("hidden");

  if (tab === "dashboard") refreshDashboard();
  if (tab === "inventory") refreshInventory();
  if (tab === "recipes") refreshRecipes();
}

async function refreshDashboard() {
  try {
    const data = await api("/api/v1/dashboard");
    els.metricTotal.textContent = data.totalProducts;
    els.metricExpiring.textContent = data.expiringSoonCount;
    els.metricUsed.textContent = data.usedThisMonth;
    els.metricWaste.textContent = data.wasteThisMonth;
  } catch (err) {
    showToast(err.message);
  }
}

function inventoryCard(product) {
  const batchesHtml = product.batches
    .map((batch) => {
      const disabled = batch.quantity <= 0 ? "disabled" : "";
      return `
        <div class="batch-item">
          <div>
            <strong>${batch.expiryDate}</strong>
            <p>Qty: ${batch.quantity} | Status: ${batch.status}</p>
            <p class="muted">Batch: ${batch.batchId}</p>
          </div>
          <button class="btn ghost consume-btn" data-batch-id="${batch.batchId}" ${disabled}>Use 1</button>
        </div>
      `;
    })
    .join("");

  return `
    <article class="product-card">
      <div class="product-head">
        <div>
          <h4>${product.name}</h4>
          <p class="muted">${product.categoryName}</p>
        </div>
        <span class="chip">Total ${product.totalQuantity}</span>
      </div>
      <div class="batch-list">${batchesHtml || "<p class='muted'>No batches</p>"}</div>
    </article>
  `;
}

async function refreshInventory() {
  try {
    const categoryQuery = state.category ? `&category=${encodeURIComponent(state.category)}` : "";
    const data = await api(`/api/v1/products?page=${state.page}&pageSize=${state.pageSize}${categoryQuery}`);
    els.inventoryList.innerHTML = data.items.length ? data.items.map(inventoryCard).join("") : "<p class='muted'>No products yet.</p>";

    document.querySelectorAll(".consume-btn").forEach((btn) => {
      btn.addEventListener("click", async () => {
        try {
          await api("/api/v1/products/consume", {
            method: "POST",
            body: JSON.stringify({ batchId: btn.dataset.batchId, quantityUsed: 1 })
          });
          showToast("Batch quantity reduced");
          refreshInventory();
          refreshDashboard();
        } catch (err) {
          showToast(err.message);
        }
      });
    });
  } catch (err) {
    showToast(err.message);
  }
}

async function refreshRecipes() {
  try {
    const recipes = await api("/api/v1/recipes/suggestions");
    els.recipeList.innerHTML = recipes.length
      ? recipes
          .map(
            (recipe) => `
          <article class="recipe-card">
            <h4>${recipe.name}</h4>
            <p class="muted">Match: ${recipe.matchPercent}% | Expiry boost: ${recipe.expiryPriorityScore}</p>
            <p><strong>Final Score:</strong> ${recipe.finalScore}</p>
          </article>`
          )
          .join("")
      : "<p class='muted'>No suggestions yet.</p>";
  } catch (err) {
    showToast(err.message);
  }
}

function wireEvents() {
  document.querySelectorAll(".auth-tab").forEach((btn) => {
    btn.addEventListener("click", () => switchAuthTab(btn.dataset.authTab));
  });

  document.querySelectorAll(".section-tab").forEach((btn) => {
    btn.addEventListener("click", () => switchMainTab(btn.dataset.tab));
  });

  els.loginForm.addEventListener("submit", async (e) => {
    e.preventDefault();

    const email = document.getElementById("login-email").value;
    const password = document.getElementById("login-password").value;

    try {
      const res = await api("/api/v1/auth/login", {
        method: "POST",
        body: JSON.stringify({ email, password })
      });
      setAuth(res.accessToken, res.email);
      showToast("Welcome back");
    } catch (err) {
      showToast(err.message);
    }
  });

  els.registerForm.addEventListener("submit", async (e) => {
    e.preventDefault();

    const email = document.getElementById("register-email").value;
    const password = document.getElementById("register-password").value;

    try {
      const res = await api("/api/v1/auth/register", {
        method: "POST",
        body: JSON.stringify({ email, password })
      });
      setAuth(res.accessToken, res.email);
      showToast("Account created");
    } catch (err) {
      showToast(err.message);
    }
  });

  els.addProductForm.addEventListener("submit", async (e) => {
    e.preventDefault();

    const body = {
      name: document.getElementById("p-name").value,
      categoryName: document.getElementById("p-category").value,
      expiryDate: document.getElementById("p-expiry").value,
      quantity: Number(document.getElementById("p-qty").value)
    };

    try {
      await api("/api/v1/products", { method: "POST", body: JSON.stringify(body) });
      showToast("Product batch added");
      els.addProductForm.reset();
      refreshInventory();
      refreshDashboard();
    } catch (err) {
      showToast(err.message);
    }
  });

  els.applyFilter.addEventListener("click", () => {
    state.category = els.categoryFilter.value.trim();
    state.page = 1;
    refreshInventory();
  });

  els.scanForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    const payload = {
      rawText: document.getElementById("ocr-text").value,
      quantity: Number(document.getElementById("ocr-qty").value)
    };

    try {
      const result = await api("/api/v1/ocr/scan", { method: "POST", body: JSON.stringify(payload) });
      showToast(`Added ${result.name}`);
      els.scanForm.reset();
      refreshInventory();
      refreshDashboard();
    } catch (err) {
      showToast(err.message);
    }
  });

  els.correctForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    const payload = {
      batchId: document.getElementById("c-batch").value,
      originalExpiryDate: document.getElementById("c-original").value,
      correctedExpiryDate: document.getElementById("c-corrected").value,
      rawOcrText: document.getElementById("c-raw").value
    };

    try {
      await api("/api/v1/ocr/correct-date", { method: "POST", body: JSON.stringify(payload) });
      showToast("OCR correction saved");
      refreshInventory();
    } catch (err) {
      showToast(err.message);
    }
  });

  els.refreshRecipes.addEventListener("click", refreshRecipes);

  els.logoutBtn.addEventListener("click", () => {
    clearAuth();
    showToast("Signed out");
  });
}

wireEvents();
syncAuthUi();
switchMainTab("dashboard");
