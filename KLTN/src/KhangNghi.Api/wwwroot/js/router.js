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
    "multi-dim": "crossAnalysis.view",
    "olap-explorer": "crossAnalysis.view",
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

    // Redirect mdx-studio to question-explorer
    if (rawHash === "mdx-studio") {
      window.location.hash = "#/question-explorer";
      return;
    }

    // Redirect olap-explorer to multi-dim
    if (rawHash === "olap-explorer") {
      window.location.hash = "#/multi-dim";
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
    // Đóng sidebar dạng ngăn kéo trên mobile sau khi chọn trang
    if (sidebar) sidebar.classList.remove("mobile-open");

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
      "login": { cat: "Bảo mật", title: "Đăng nhập" },
      "403": { cat: "Bảo mật", title: "Truy cập bị từ chối" },
      "404": { cat: "Hệ thống", title: "Không tìm thấy trang" },
      "overview": { cat: "Tổng quan", title: "Tổng quan" },
      "sales": { cat: "Kinh doanh", title: "Bán hàng" },
      "purchase": { cat: "Kinh doanh", title: "Mua hàng" },
      "inventory": { cat: "Kinh doanh", title: "Tồn kho" },
      "shipping": { cat: "Kinh doanh", title: "Vận chuyển" },
      "customer": { cat: "Đối tượng", title: "Khách hàng" },
      "product": { cat: "Đối tượng", title: "Sản phẩm" },
      "supplier": { cat: "Đối tượng", title: "Nhà cung cấp" },
      "geography": { cat: "Đối tượng", title: "Khu vực" },
      "cross-analysis": { cat: "Phân tích", title: "Phân tích chéo" },
      "multi-dim": { cat: "Phân tích", title: "Phân tích đa chiều" },
      "olap-explorer": { cat: "Phân tích", title: "Phân tích đa chiều" },
      "forecast": { cat: "Dự báo", title: "Dự báo doanh số" },
      "what-if": { cat: "Phân tích", title: "Phân tích giả định" },
      "question-explorer": { cat: "Phân tích", title: "Câu hỏi phân tích" },
      "system-status": { cat: "Hệ thống", title: "Trạng thái dữ liệu" },
      "etl-logs": { cat: "Hệ thống", title: "Nhật ký ETL" },
      "settings": { cat: "Hệ thống", title: "Cài đặt" }
    };

    const item = meta[route] || { cat: "", title: "" };
    // Không lặp lại khi nhóm trùng tên trang (vd. Tổng quan)
    const showCat = item.cat && item.cat !== item.title;
    if (breadcrumbCategory) {
      breadcrumbCategory.textContent = showCat ? item.cat : "";
      breadcrumbCategory.hidden = !showCat;
      const sep = breadcrumbCategory.nextElementSibling;
      if (sep) sep.hidden = !showCat;
    }
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

    const roleSelect = document.getElementById("demo-role-select");
    if (roleSelect) roleSelect.value = user.role;

    // Sidebar Items Visibility based on RBAC Permissions & Status Badges
    document.querySelectorAll("#sidebar .nav-item").forEach(el => {
      const href = el.getAttribute("href") || "";
      const routeKey = href.replace("#/", "");
      const requiredPerm = ROUTE_PERMISSIONS[routeKey];
      // Tooltip gốc của trình duyệt cho sidebar thu gọn (tooltip CSS bị vùng cuộn của menu cắt mất)
      if (el.dataset.tooltip) el.title = el.dataset.tooltip;
      
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

  function navigate(route) {
    const cleanRoute = (route || "").replace(/^#\/?/, "");
    const targetHash = `#/${cleanRoute}`;
    if (window.location.hash === targetHash) {
      handleHashChange();
    } else {
      window.location.hash = targetHash;
    }
  }

  return {
    init,
    navigate,
    handleHashChange,
    updateNavigationUI,
    onRouteChanged,
    getCurrentRoute,
    ROUTE_PERMISSIONS
  };
})();
