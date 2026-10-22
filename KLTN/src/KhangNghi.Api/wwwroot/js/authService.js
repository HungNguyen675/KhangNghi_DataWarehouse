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

  // Deterministic Demo Accounts with Distinct Role-Specific Passwords
  const DEMO_ACCOUNTS = [
    {
      userId: "USR-001",
      email: "ceo@khangnghi.demo",
      password: "Ceo@KhangNghi2026!",
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
      password: "Sales@KhangNghi2026$",
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
      password: "Purchase@KhangNghi2026#",
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
      password: "Warehouse@KhangNghi2026%",
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
      password: "Shipping@KhangNghi2026&",
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
      password: "Admin@KhangNghi2026#",
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
        const matched = DEMO_ACCOUNTS.find(a => 
          a.email.toLowerCase() === (parsed.email || "").toLowerCase() || 
          a.email.replace(".demo", ".com.vn").toLowerCase() === (parsed.email || "").toLowerCase() ||
          a.role.toLowerCase() === (parsed.role || "").toLowerCase()
        );
        if (matched) {
          currentUser = { ...matched, lastLogin: parsed.loginTime || new Date().toLocaleTimeString("vi-VN") };
        } else if (parsed.email && parsed.role) {
          currentUser = {
            userId: "USR-SESSION",
            email: parsed.email,
            fullName: parsed.email.split("@")[0],
            role: parsed.role,
            roleTitle: parsed.role === "Admin" ? "Quản Trị Hệ Thống" : "Ban Giám Đốc",
            department: "Hệ Thống DWH Khang Nghị",
            avatar: parsed.role.substring(0, 2).toUpperCase(),
            lastLogin: parsed.loginTime || new Date().toLocaleTimeString("vi-VN")
          };
        }
      } catch (e) {
        console.error("Auth session parse error:", e);
      }
    }
  }

  // "Ban Giám Đốc Khang Nghị" -> "BN"; "admin" -> "AD"
  function initialsOf(name) {
    const words = String(name).trim().split(/\s+/).filter(Boolean);
    if (words.length >= 2) return (words[0][0] + words[words.length - 1][0]).toUpperCase();
    return String(name).substring(0, 2).toUpperCase();
  }

  async function login(emailOrUsername, password, rememberMe = false) {
    const client = window.KhangNghiApiClient;
    const input = (emailOrUsername || "").trim();

    // Mapping alias demo accounts sang tài khoản thực tế trong CSDL Sys_User
    const aliasMap = {
      "ceo@khangnghi.demo": "ceo@khangnghi.com.vn",
      "sales@khangnghi.demo": "sales@khangnghi.com.vn",
      "purchase@khangnghi.demo": "purchase@khangnghi.com.vn",
      "warehouse@khangnghi.demo": "kho@khangnghi.com.vn",
      "logistics@khangnghi.demo": "shipping@khangnghi.com.vn",
      "admin@khangnghi.demo": "admin@khangnghi.com.vn"
    };
    const apiIdentifier = aliasMap[input.toLowerCase()] || input.replace(".demo", ".com.vn");

    // 1. Attempt Real Backend JWT Login
    if (client) {
      const res = await client.post("/auth/login", { username: apiIdentifier, email: apiIdentifier, password });
      if (res.success && res.data && res.data.token) {
        client.saveToken(res.data.token, rememberMe);
        const nowStr = new Date().toLocaleString("vi-VN");
        
        const rawRole = res.data.role || "Director";
        let mappedRole = "CEO";
        let roleTitle = "Ban Giám Đốc";
        if (rawRole === "Admin") {
          mappedRole = "Admin";
          roleTitle = "Quản Trị Hệ Thống";
        } else if (rawRole === "Director" || rawRole === "CEO") {
          mappedRole = "CEO";
          roleTitle = "Ban Giám Đốc";
        } else if (rawRole === "SalesManager" || rawRole === "Sales") {
          mappedRole = "Sales";
          roleTitle = "Trưởng Phòng Kinh Doanh";
        } else if (rawRole === "PurchaseManager" || rawRole === "Purchase") {
          mappedRole = "Purchase";
          roleTitle = "Trưởng Phòng Mua Hàng";
        } else if (rawRole === "InventoryManager" || rawRole === "Warehouse") {
          mappedRole = "Warehouse";
          roleTitle = "Thủ Kho Trưởng";
        } else if (rawRole === "ShippingStaff" || rawRole === "Logistics") {
          mappedRole = "Logistics";
          roleTitle = "Trưởng Phòng Logistics";
        }

        currentUser = {
          userId: `USR-${res.data.username || "LIVE"}`,
          email: res.data.email || (res.data.username + "@khangnghi.com.vn"),
          fullName: res.data.fullName || res.data.username,
          role: mappedRole,
          roleTitle: roleTitle,
          department: "Hệ Thống DWH Khang Nghị",
          avatar: initialsOf(res.data.fullName || res.data.username || "KN"),
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
      a.email.replace(".demo", ".com.vn").toLowerCase() === lowerInput ||
      a.role.toLowerCase() === lowerInput ||
      (lowerInput === "giamdoc" && a.role === "CEO") ||
      (lowerInput === "kinhdoanh" && a.role === "Sales") ||
      (lowerInput === "muahang" && a.role === "Purchase") ||
      (lowerInput === "kho" && a.role === "Warehouse") ||
      (lowerInput === "vanchuyen" && a.role === "Logistics") ||
      (lowerInput === "admin" && a.role === "Admin")
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
    if (window.KhangNghiApiClient) window.KhangNghiApiClient.clearToken();
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

  // Đăng nhập lại bằng tài khoản demo của vai trò đích để JWT khớp với vai trò (Backend kiểm tra quyền theo token)
  async function switchDemoRole(roleName) {
    const targetAccount = DEMO_ACCOUNTS.find(a => a.role.toLowerCase() === roleName.toLowerCase());
    if (!targetAccount) return false;
    const result = await login(targetAccount.email, targetAccount.password, false);
    return result.success;
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
