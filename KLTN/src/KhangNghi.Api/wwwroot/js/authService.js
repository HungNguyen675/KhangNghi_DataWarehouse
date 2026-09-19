/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - AUTHENTICATION & RBAC SERVICE (PROTOTYPE)
   Architecture Abstraction: Ready for future ASP.NET Core JWT & Sys_User integration
   ========================================================================== */

window.KhangNghiAuth = (function () {
  // Granular Permissions
  const PERMISSIONS = {
    DASHBOARD_OVERVIEW: "dashboard.overview",
    SALES_VIEW: "sales.view",
    PURCHASE_VIEW: "purchase.view",
    INVENTORY_VIEW: "inventory.view",
    SHIPPING_VIEW: "shipping.view",
    CUSTOMERS_VIEW: "customers.view",
    PRODUCTS_VIEW: "products.view",
    SUPPLIERS_VIEW: "suppliers.view",
    GEOGRAPHY_VIEW: "geography.view",
    CROSS_ANALYSIS_VIEW: "crossAnalysis.view",
    FORECAST_VIEW: "forecast.view",
    WHAT_IF_VIEW: "whatIf.view",
    BI_QUESTIONS_VIEW: "biQuestions.view",
    COPILOT_USE: "copilot.use",
    SYSTEM_STATUS_VIEW: "systemStatus.view"
  };

  // 5 Business Roles (+ System Admin) Matrix
  const ROLE_PERMISSIONS = {
    "CEO": [
      PERMISSIONS.DASHBOARD_OVERVIEW,
      PERMISSIONS.SALES_VIEW,
      PERMISSIONS.PURCHASE_VIEW,
      PERMISSIONS.INVENTORY_VIEW,
      PERMISSIONS.SHIPPING_VIEW,
      PERMISSIONS.CUSTOMERS_VIEW,
      PERMISSIONS.PRODUCTS_VIEW,
      PERMISSIONS.SUPPLIERS_VIEW,
      PERMISSIONS.GEOGRAPHY_VIEW,
      PERMISSIONS.CROSS_ANALYSIS_VIEW,
      PERMISSIONS.FORECAST_VIEW,
      PERMISSIONS.WHAT_IF_VIEW,
      PERMISSIONS.BI_QUESTIONS_VIEW,
      PERMISSIONS.COPILOT_USE,
      PERMISSIONS.SYSTEM_STATUS_VIEW
    ],
    "Sales": [
      PERMISSIONS.DASHBOARD_OVERVIEW,
      PERMISSIONS.SALES_VIEW,
      PERMISSIONS.CUSTOMERS_VIEW,
      PERMISSIONS.PRODUCTS_VIEW,
      PERMISSIONS.GEOGRAPHY_VIEW,
      PERMISSIONS.FORECAST_VIEW,
      PERMISSIONS.WHAT_IF_VIEW,
      PERMISSIONS.BI_QUESTIONS_VIEW,
      PERMISSIONS.COPILOT_USE
    ],
    "Purchase": [
      PERMISSIONS.DASHBOARD_OVERVIEW,
      PERMISSIONS.PURCHASE_VIEW,
      PERMISSIONS.SUPPLIERS_VIEW,
      PERMISSIONS.PRODUCTS_VIEW,
      PERMISSIONS.BI_QUESTIONS_VIEW,
      PERMISSIONS.COPILOT_USE
    ],
    "Warehouse": [
      PERMISSIONS.DASHBOARD_OVERVIEW,
      PERMISSIONS.INVENTORY_VIEW,
      PERMISSIONS.PRODUCTS_VIEW,
      PERMISSIONS.BI_QUESTIONS_VIEW,
      PERMISSIONS.COPILOT_USE
    ],
    "Logistics": [
      PERMISSIONS.DASHBOARD_OVERVIEW,
      PERMISSIONS.SHIPPING_VIEW,
      PERMISSIONS.GEOGRAPHY_VIEW,
      PERMISSIONS.BI_QUESTIONS_VIEW,
      PERMISSIONS.COPILOT_USE
    ],
    "Admin": [
      PERMISSIONS.DASHBOARD_OVERVIEW,
      PERMISSIONS.SALES_VIEW,
      PERMISSIONS.PURCHASE_VIEW,
      PERMISSIONS.INVENTORY_VIEW,
      PERMISSIONS.SHIPPING_VIEW,
      PERMISSIONS.CUSTOMERS_VIEW,
      PERMISSIONS.PRODUCTS_VIEW,
      PERMISSIONS.SUPPLIERS_VIEW,
      PERMISSIONS.GEOGRAPHY_VIEW,
      PERMISSIONS.CROSS_ANALYSIS_VIEW,
      PERMISSIONS.FORECAST_VIEW,
      PERMISSIONS.WHAT_IF_VIEW,
      PERMISSIONS.BI_QUESTIONS_VIEW,
      PERMISSIONS.COPILOT_USE,
      PERMISSIONS.SYSTEM_STATUS_VIEW
    ]
  };

  // Deterministic Demo Accounts
  const DEMO_ACCOUNTS = [
    {
      userId: "USR-001",
      email: "ceo@khangnghi.demo",
      password: "123456",
      fullName: "Bùi Thị Vấn",
      role: "CEO",
      roleTitle: "Ban Giám Đốc (CEO)",
      department: "Ban Giám Đốc",
      avatar: "BV",
      lastLogin: "Vừa xong"
    },
    {
      userId: "USR-002",
      email: "sales@khangnghi.demo",
      password: "123456",
      fullName: "Nguyễn Văn Nam",
      role: "Sales",
      roleTitle: "Trưởng Phòng Kinh Doanh",
      department: "Phòng Bán Hàng",
      avatar: "NN",
      lastLogin: "Vừa xong"
    },
    {
      userId: "USR-003",
      email: "purchase@khangnghi.demo",
      password: "123456",
      fullName: "Trần Thị Hoa",
      role: "Purchase",
      roleTitle: "Trưởng Phòng Mua Hàng",
      department: "Phòng Mua Hàng & NCC",
      avatar: "TH",
      lastLogin: "Vừa xong"
    },
    {
      userId: "USR-004",
      email: "warehouse@khangnghi.demo",
      password: "123456",
      fullName: "Phạm Hoàng Long",
      role: "Warehouse",
      roleTitle: "Thủ Kho Trưởng",
      department: "Phòng Quản Lý Kho",
      avatar: "PL",
      lastLogin: "Vừa xong"
    },
    {
      userId: "USR-005",
      email: "logistics@khangnghi.demo",
      password: "123456",
      fullName: "Lê Văn Đức",
      role: "Logistics",
      roleTitle: "Trưởng Phòng Logistics",
      department: "Phòng Vận Chuyển & SLA",
      avatar: "LĐ",
      lastLogin: "Vừa xong"
    },
    {
      userId: "USR-000",
      email: "admin@khangnghi.demo",
      password: "123456",
      fullName: "Quản Trị Viên",
      role: "Admin",
      roleTitle: "Quản Trị Hệ Thống",
      department: "Phòng CNTT & DWH",
      avatar: "AD",
      lastLogin: "Vừa xong"
    }
  ];

  const SESSION_KEY = "kn_bi_session";
  let currentUser = null;
  const authStateCallbacks = [];

  function init() {
    // Restore session from sessionStorage or localStorage (Remember Me)
    const stored = sessionStorage.getItem(SESSION_KEY) || localStorage.getItem(SESSION_KEY);
    if (stored) {
      try {
        const parsed = JSON.parse(stored);
        const matched = DEMO_ACCOUNTS.find(a => a.email.toLowerCase() === (parsed.email || "").toLowerCase() || a.role.toLowerCase() === (parsed.role || "").toLowerCase());
        if (matched) {
          currentUser = { ...matched, lastLogin: parsed.loginTime || new Date().toLocaleTimeString("vi-VN") };
        }
      } catch (e) {
        console.error("Auth session parse error:", e);
      }
    }
  }

  async function login(emailOrUsername, password, rememberMe = false) {
    const client = window.KhangNghiApiClient;
    const input = (emailOrUsername || "").trim();

    // 1. Attempt Real Backend JWT Login
    if (client) {
      const res = await client.post("/auth/login", { username: input, password });
      if (res.success && res.data && res.data.token) {
        client.saveToken(res.data.token, rememberMe);
        const nowStr = new Date().toLocaleString("vi-VN");
        
        const role = res.data.role || "Director";
        currentUser = {
          userId: "USR-LIVE",
          email: res.data.username + "@khangnghi.com",
          fullName: res.data.fullName || res.data.username,
          role: role === "Admin" ? "Admin" : role === "Director" ? "CEO" : role,
          roleTitle: role === "Admin" ? "Quản Trị Hệ Thống" : "Ban Giám Đốc",
          department: "DWH System",
          avatar: (res.data.fullName || "KN").substring(0, 2).toUpperCase(),
          lastLogin: nowStr
        };

        const sessionPayload = JSON.stringify({
          email: currentUser.email,
          role: currentUser.role,
          loginTime: nowStr
        });

        if (rememberMe) {
          localStorage.setItem(SESSION_KEY, sessionPayload);
        } else {
          sessionStorage.setItem(SESSION_KEY, sessionPayload);
        }

        notifyAuthStateChanged();
        return { success: true, user: currentUser };
      }
    }

    // 2. Fallback to Local Demo Accounts
    const lowerInput = input.toLowerCase();
    const account = DEMO_ACCOUNTS.find(a => 
      a.email.toLowerCase() === lowerInput || 
      a.role.toLowerCase() === lowerInput ||
      (lowerInput === "giamdoc" && a.role === "CEO") ||
      (lowerInput === "kinhdoanh" && a.role === "Sales") ||
      (lowerInput === "muahang" && a.role === "Purchase") ||
      (lowerInput === "kho" && a.role === "Warehouse") ||
      (lowerInput === "vanchuyen" && a.role === "Logistics")
    );

    if (!account) {
      return { success: false, message: "Tên đăng nhập hoặc email không tồn tại." };
    }

    if (account.password !== password) {
      return { success: false, message: "Mật khẩu không chính xác." };
    }

    const nowStr = new Date().toLocaleString("vi-VN");
    currentUser = { ...account, lastLogin: nowStr };

    const sessionPayload = JSON.stringify({
      email: account.email,
      role: account.role,
      loginTime: nowStr
    });

    if (rememberMe) {
      localStorage.setItem(SESSION_KEY, sessionPayload);
    } else {
      sessionStorage.setItem(SESSION_KEY, sessionPayload);
    }

    notifyAuthStateChanged();
    return { success: true, user: currentUser };
  }

  function logout() {
    currentUser = null;
    sessionStorage.removeItem(SESSION_KEY);
    localStorage.removeItem(SESSION_KEY);
    notifyAuthStateChanged();
    window.location.hash = "#/login";
  }

  function getCurrentUser() {
    return currentUser;
  }

  function isAuthenticated() {
    return currentUser !== null;
  }

  function hasPermission(permissionId) {
    if (!currentUser) return false;
    const userRole = currentUser.role;
    const permissions = ROLE_PERMISSIONS[userRole] || [];
    return permissions.includes(permissionId);
  }

  function hasRole(roleId) {
    if (!currentUser) return false;
    return currentUser.role === roleId;
  }

  function switchDemoRole(roleName) {
    const targetAccount = DEMO_ACCOUNTS.find(a => a.role.toLowerCase() === roleName.toLowerCase());
    if (targetAccount) {
      const nowStr = new Date().toLocaleString("vi-VN");
      currentUser = { ...targetAccount, lastLogin: nowStr };
      const sessionPayload = JSON.stringify({
        email: targetAccount.email,
        role: targetAccount.role,
        loginTime: nowStr
      });
      sessionStorage.setItem(SESSION_KEY, sessionPayload);
      notifyAuthStateChanged();
      return true;
    }
    return false;
  }

  function onAuthStateChanged(callback) {
    authStateCallbacks.push(callback);
  }

  function notifyAuthStateChanged() {
    authStateCallbacks.forEach(cb => cb(currentUser));
  }

  // Self-initialize
  init();

  return {
    PERMISSIONS,
    DEMO_ACCOUNTS,
    login,
    logout,
    getCurrentUser,
    isAuthenticated,
    hasPermission,
    hasRole,
    switchDemoRole,
    onAuthStateChanged
  };
})();
