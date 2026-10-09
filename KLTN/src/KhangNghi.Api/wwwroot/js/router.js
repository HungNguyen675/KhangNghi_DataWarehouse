/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - SPA ROUTER & RBAC ROUTE GUARD
   Client-Side Routing with Auth Protection, Permission Guards, 403 & 404 Handling
   ========================================================================== */

window.KhangNghiRouter = (function () {
  const ROUTE_PERMISSIONS = {
    "overview": "dashboard.overview",
    "sales": "sales.view",
    "purchase": "purchase.view",
    "inventory": "inventory.view",
    "shipping": "shipping.view",
    "customer": "customers.view",
    "product": "products.view",
    "supplier": "suppliers.view",
    "geography": "geography.view",
    "cross-analysis": "crossAnalysis.view",
    "forecast": "forecast.view",
    "what-if": "whatIf.view",
    "question-explorer": "biQuestions.view",
    "system-status": "systemStatus.view",
    "etl-logs": "systemStatus.view",
    "settings": "dashboard.overview"
  };

  const VALID_ROUTES = Object.keys(ROUTE_PERMISSIONS);

  let currentRoute = "overview";
  const routeChangeCallbacks = [];

  function init() {
    window.addEventListener("hashchange", handleHashChange);
    if (window.KhangNghiAuth) {
      window.KhangNghiAuth.onAuthStateChanged(() => {
        updateNavigationUI();
        handleHashChange();
      });
    }
    handleHashChange();
  }

  function handleHashChange() {
    const rawHash = (window.location.hash || "").replace("#/", "").replace("#", "");
    const auth = window.KhangNghiAuth;

    // 1. Check Authentication Status
    if (!auth || !auth.isAuthenticated()) {
      if (rawHash !== "login") {
        window.location.hash = "#/login";
        return;
      }
      renderRoute("login");
      return;
    }

    // If authenticated & on login page -> redirect to overview
    if (rawHash === "login" || rawHash === "") {
      window.location.hash = "#/overview";
      return;
    }

    // 2. Validate Route Existance
    if (!VALID_ROUTES.includes(rawHash)) {
      renderRoute("404");
      return;
    }

    // 3. Permission Guard Check
    const requiredPermission = ROUTE_PERMISSIONS[rawHash];
    if (requiredPermission && !auth.hasPermission(requiredPermission)) {
      renderRoute("403", rawHash);
      return;
    }

    // 4. Allowed -> Render View
    renderRoute(rawHash);
  }

  function renderRoute(route, attemptedRoute = "") {
    currentRoute = route;

    const appShell = document.getElementById("app-shell");
    const sidebar = document.getElementById("sidebar");
    const topbar = document.getElementById("topbar");

    // 1. Toggle App Shell Layout for Login vs App
    if (route === "login") {
      if (appShell) appShell.classList.add("login-mode");
      if (sidebar) sidebar.style.display = "none";
      if (topbar) topbar.style.display = "none";
    } else {
      if (appShell) appShell.classList.remove("login-mode");
      if (sidebar) sidebar.style.display = "flex";
      if (topbar) topbar.style.display = "flex";
    }

    // 2. Hide all view sections, activate target view
    document.querySelectorAll(".view-section").forEach(el => {
      if (el.id === `view-${route}`) {
        el.classList.add("active");
        el.style.display = "block";
      } else {
        el.classList.remove("active");
        el.style.display = "none";
      }
    });

    // Handle 403 context display if applicable
    if (route === "403") {
      const auth = window.KhangNghiAuth;
      const user = auth ? auth.getCurrentUser() : null;
      const roleDisplay = document.getElementById("403-current-role");
      const targetDisplay = document.getElementById("403-attempted-route");
      if (roleDisplay && user) roleDisplay.textContent = `${user.roleTitle} (${user.role})`;
      if (targetDisplay) targetDisplay.textContent = `/${attemptedRoute}`;
    }

    // 3. Highlight Active Nav Item
    document.querySelectorAll(".nav-item").forEach(el => {
      const href = el.getAttribute("href") || "";
      if (href.endsWith(route)) {
        el.classList.add("active");
      } else {
        el.classList.remove("active");
      }
    });

    // 4. Update Topbar Breadcrumb & Profile Header
    updateNavigationUI();

    const breadcrumbCategory = document.getElementById("breadcrumb-category");
    const breadcrumbCurrent = document.getElementById("breadcrumb-current");
    
    const meta = {
      "login": { cat: "Bảo mật / Security", title: "Hệ thống đăng nhập / Login System" },
      "403": { cat: "Bảo mật / Security", title: "403 Truy cập bị từ chối / Access Denied" },
      "404": { cat: "Hệ thống / System", title: "404 Không tìm thấy trang / Not Found" },
      "overview": { cat: "Tổng quan / Executive Overview", title: "Tổng quan / Executive Overview" },
      "sales": { cat: "Phân tích kinh doanh / Business Analytics", title: "Bán hàng / Sales" },
      "purchase": { cat: "Phân tích kinh doanh / Business Analytics", title: "Mua hàng / Purchase" },
      "inventory": { cat: "Phân tích kinh doanh / Business Analytics", title: "Tồn kho / Inventory" },
      "shipping": { cat: "Phân tích kinh doanh / Business Analytics", title: "Vận chuyển / Shipping" },
      "customer": { cat: "Phân tích đối tượng / Master Analysis", title: "Khách hàng / Customers" },
      "product": { cat: "Phân tích đối tượng / Master Analysis", title: "Sản phẩm / Products" },
      "supplier": { cat: "Phân tích đối tượng / Master Analysis", title: "Nhà cung cấp / Suppliers" },
      "geography": { cat: "Phân tích đối tượng / Master Analysis", title: "Khu vực / Geography" },
      "cross-analysis": { cat: "Phân tích nâng cao / Advanced Analytics", title: "Phân tích chéo / Cross Analysis" },
      "forecast": { cat: "Phân tích nâng cao / Advanced Analytics", title: "Dự báo / Forecast" },
      "what-if": { cat: "Phân tích nâng cao / Advanced Analytics", title: "Phân tích giả định / What-If" },
      "question-explorer": { cat: "Trí tuệ dữ liệu / Data Intelligence", title: "Câu hỏi BI / BI Questions" },
      "system-status": { cat: "Hệ thống / System", title: "Trạng thái dữ liệu / Data Status" },
      "etl-logs": { cat: "Hệ thống / System", title: "Nhật ký ETL / ETL Logs" },
      "settings": { cat: "Hệ thống / System", title: "Cài đặt / Settings" }
    };

    const item = meta[route] || { cat: "Dashboard", title: "Analytics" };
    if (breadcrumbCategory) breadcrumbCategory.textContent = item.cat;
    if (breadcrumbCurrent) breadcrumbCurrent.textContent = item.title;

    window.scrollTo({ top: 0, behavior: 'smooth' });
    routeChangeCallbacks.forEach(fn => fn(route));
  }

  function updateNavigationUI() {
    const auth = window.KhangNghiAuth;
    if (!auth) return;

    const user = auth.getCurrentUser();
    if (!user) return;

    // Update Profile elements
    const avatarEls = document.querySelectorAll(".user-avatar-text");
    const nameEls = document.querySelectorAll(".user-name-text");
    const roleEls = document.querySelectorAll(".user-role-text");
    const deptEls = document.querySelectorAll(".user-dept-text");

    avatarEls.forEach(el => el.textContent = user.avatar);
    nameEls.forEach(el => el.textContent = user.fullName);
    roleEls.forEach(el => el.textContent = user.roleTitle);
    deptEls.forEach(el => el.textContent = user.department);

    // Sidebar Items Visibility based on RBAC Permissions
    document.querySelectorAll("#sidebar .nav-item").forEach(el => {
      const href = el.getAttribute("href") || "";
      const routeKey = href.replace("#/", "");
      const requiredPerm = ROUTE_PERMISSIONS[routeKey];
      
      if (!requiredPerm || auth.hasPermission(requiredPerm)) {
        el.style.display = "flex";
      } else {
        el.style.display = "none";
      }
    });

    // Hide empty menu group headers
    document.querySelectorAll("#sidebar .sidebar-menu").forEach(menuEl => {
      let currentHeader = null;
      let visibleCount = 0;
      
      Array.from(menuEl.children).forEach(child => {
        if (child.classList.contains("menu-group-header")) {
          if (currentHeader) {
            currentHeader.style.display = visibleCount > 0 ? "block" : "none";
          }
          currentHeader = child;
          visibleCount = 0;
        } else if (child.classList.contains("nav-item")) {
          if (child.style.display !== "none") {
            visibleCount++;
          }
        }
      });
      if (currentHeader) {
        currentHeader.style.display = visibleCount > 0 ? "block" : "none";
      }
    });
  }

  function onRouteChanged(callback) {
    routeChangeCallbacks.push(callback);
  }

  function getCurrentRoute() {
    return currentRoute;
  }

  return {
    init,
    updateNavigationUI,
    onRouteChanged,
    getCurrentRoute
  };
})();
