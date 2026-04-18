var PFM = (function () {
  var RAPID_HOST = 'currency-conversion-and-exchange-rates.p.rapidapi.com';
  var RAPID_KEY  = '17ac984572msh4dc66bb06822766p1257f5jsn16297f76c636';

  /* ── Storage helpers ── */
  function read(key, fallback) {
    try { var d = localStorage.getItem(key); return d ? JSON.parse(d) : fallback; }
    catch (e) { return fallback; }
  }
  function write(key, value) { localStorage.setItem(key, JSON.stringify(value)); }

  function getUsers()          { return read('pfmUsers', []); }
  function saveUsers(u)        { write('pfmUsers', u); }
  function getCurrentUser()    { return read('pfmCurrentUser', null); }
  function saveCurrentUser(u)  { write('pfmCurrentUser', u); }
  function logout()            { localStorage.removeItem('pfmCurrentUser'); }
  function getProfiles()       { return read('pfmProfiles', {}); }
  function getTransactions()   { return read('pfmTransactions', []); }
  function saveTransactions(t) { write('pfmTransactions', t); }

  function roundCurrency(value) {
    return Math.round(Number(value || 0) * 100) / 100;
  }

  function requireLogin() {
    if (!getCurrentUser()) window.location.href = 'authentication.html';
  }

  /* ── Profile ── */
  function getProfile() {
    var cur = getCurrentUser();
    if (!cur) return null;
    var profiles = getProfiles();
    if (!profiles[cur.email]) {
      profiles[cur.email] = {
        fullName: cur.fullName || '', username: cur.username || '',
        email: cur.email, phone: '', age: '', occupation: '',
        currency: 'EUR', savingsGoal: 0, totalSavings: 0,
        createdAt: new Date().toISOString()
      };
      write('pfmProfiles', profiles);
    }
    return profiles[cur.email];
  }

  function saveProfile(data) {
    var cur = getCurrentUser();
    if (!cur) return;
    var profiles = getProfiles();
    profiles[cur.email] = data;
    write('pfmProfiles', profiles);
    var users = getUsers();
    var idx = users.findIndex(function (u) { return u.email === cur.email; });
    if (idx !== -1) {
      users[idx].fullName = data.fullName;
      users[idx].username = data.username;
      saveUsers(users);
    }
    saveCurrentUser({ fullName: data.fullName, username: data.username, email: cur.email });
  }

  /* ── Transactions ── */
  function getUserTransactions() {
    var cur = getCurrentUser();
    if (!cur) return [];
    return getTransactions().filter(function (t) { return t.userEmail === cur.email; });
  }

  function uid() { return Date.now() + Math.floor(Math.random() * 100000); }

  function normalizeTransaction(d) {
    return {
      id: d.id || uid(), userEmail: getCurrentUser().email,
      date: d.date, type: d.type, category: d.category,
      description: d.description, amount: Number(d.amount),
      status: d.status, createdAt: d.createdAt || new Date().toISOString()
    };
  }

  function addTransaction(item) {
    var items = getTransactions();
    items.push(normalizeTransaction(item));
    saveTransactions(items);
  }

  function updateTransaction(id, updates) {
    var items = getTransactions();
    var idx = items.findIndex(function (t) { return Number(t.id) === Number(id); });
    if (idx === -1) return false;
    items[idx] = normalizeTransaction($.extend({}, items[idx], updates, { id: items[idx].id, createdAt: items[idx].createdAt }));
    saveTransactions(items);
    return true;
  }

  function deleteTransaction(id) {
    saveTransactions(getTransactions().filter(function (t) { return Number(t.id) !== Number(id); }));
  }

  function getTransactionById(id) {
    return getUserTransactions().find(function (t) { return Number(t.id) === Number(id); }) || null;
  }

  /* ── Currency ── */
  function getCurrencyMap() {
    return {
      EUR: { symbol: '€', api: 'EUR', name: 'Euro' },
      USD: { symbol: '$', api: 'USD', name: 'US Dollar' },
      LEK: { symbol: 'L', api: 'ALL', name: 'Albanian Lek' }
    };
  }

  function formatAmount(amount, currency) {
    var map = getCurrencyMap();
    var cur = currency || ((getProfile() && getProfile().currency) || 'EUR');
    var sym = (map[cur] || map.EUR).symbol;
    return sym + Number(amount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  /* Use yesterday by default — today's data is often not yet published by the API */
  function getApiDate(dateString) {
    if (dateString) return dateString;
    var d = new Date();
    d.setDate(d.getDate() - 1);
    return d.toISOString().split('T')[0];
  }

  /* Uses $.ajax (jQuery AJAX) wrapped in a native Promise, matching the RapidAPI curl spec */
  function convertUserFinancialData(fromCurrency, toCurrency, dateString) {
    var profile = getProfile();
    var cur = getCurrentUser();

    if (!profile || !cur) {
      return Promise.reject(new Error('No logged-in user found.'));
    }

    return convertAmount(1, fromCurrency, toCurrency, dateString).then(function (result) {
      var rate = Number(result.rate || 1);
      var updatedProfile = $.extend({}, profile, {
        currency: toCurrency,
        totalSavings: roundCurrency(Number(profile.totalSavings || 0) * rate),
        savingsGoal: roundCurrency(Number(profile.savingsGoal || 0) * rate)
      });

      var allTransactions = getTransactions();
      var updatedTransactions = allTransactions.map(function (t) {
        if (t.userEmail !== cur.email) return t;
        return $.extend({}, t, {
          amount: roundCurrency(Number(t.amount || 0) * rate)
        });
      });

      saveTransactions(updatedTransactions);
      saveProfile(updatedProfile);

      return {
        rate: rate,
        date: result.date,
        from: fromCurrency,
        to: toCurrency,
        profile: updatedProfile
      };
    });
  }

  function convertAmount(amount, fromCurrency, toCurrency, dateString) {
    var map      = getCurrencyMap();
    var fromCode = (map[fromCurrency] || map.EUR).api;
    var toCode   = (map[toCurrency]   || map.EUR).api;

    if (fromCode === toCode) {
      return Promise.resolve({ rate: 1, amount: Number(amount), from: fromCurrency, to: toCurrency });
    }

    var date = getApiDate(dateString);
    var url  = 'https://' + RAPID_HOST + '/timeseries'
             + '?start_date=' + date
             + '&end_date='   + date
             + '&base='       + fromCode
             + '&symbols='    + toCode;

    return new Promise(function (resolve, reject) {
      $.ajax({
        url:    url,
        method: 'GET',
        headers: {
          'Content-Type':    'application/json',
          'x-rapidapi-host': RAPID_HOST,
          'x-rapidapi-key':  RAPID_KEY
        }
      }).done(function (data) {
        var rates = data.rates && data.rates[date];
        var rate  = rates ? rates[toCode] : null;
        if (!rate) {
          reject(new Error('No rate returned for ' + fromCode + '→' + toCode + ' on ' + date + '. Response: ' + JSON.stringify(data)));
          return;
        }
        resolve({
          rate:   Number(rate),
          amount: Number(amount) * Number(rate),
          date:   date,
          from:   fromCurrency,
          to:     toCurrency
        });
      }).fail(function (xhr) {
        var msg = (xhr.responseJSON && xhr.responseJSON.message) || xhr.statusText || 'Request failed';
        reject(new Error('API error (' + xhr.status + '): ' + msg));
      });
    });
  }

  /* ── Validation ── */
  function validateEmail(e)    { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e); }
  function validateUsername(u) { return /^[a-zA-Z0-9_]{4,20}$/.test(u); }
  function validatePassword(p) { return /^(?=.*[A-Za-z])(?=.*\d).{8,}$/.test(p); }
  function getValidationMessages() {
    return {
      email:    'Enter a valid email address such as name@example.com.',
      username: 'Username must be 4–20 characters: letters, numbers, or underscore.',
      password: 'Password must be at least 8 characters with a letter and a number.'
    };
  }

  /* ── Categories ── */
  function getCategories() {
    return {
      Income:  ['Allowance', 'Grants', 'Scholarships', 'Salary', 'Freelance', 'Gift', 'Investment', 'Other Income'],
      Expense: ['Groceries', 'Entertainment', 'Utilities', 'Tuition', 'Transport', 'Housing', 'Health', 'Dining', 'Other Expense']
    };
  }

  function fillCategoryOptions(typeSelector, categorySelector, selected) {
    var type = $(typeSelector).val();
    var list = getCategories()[type] || [];
    var html = '<option value="">Select category</option>';
    list.forEach(function (item) {
      html += '<option value="' + item + '"' + (selected === item ? ' selected' : '') + '>' + item + '</option>';
    });
    $(categorySelector).html(html);
  }

  /* ── Totals ── */
  function getTotals(transactions) {
    var income = 0, expenses = 0;
    (transactions || []).forEach(function (t) {
      if (t.type === 'Income') income += Number(t.amount || 0);
      else expenses += Number(t.amount || 0);
    });
    var savings = Number((getProfile() && getProfile().totalSavings) || 0);
    return { income: income, expenses: expenses, savings: savings, balance: income - expenses + savings };
  }

  function getMonthlyTotals(transactions) {
    var now = new Date(), m = now.getMonth(), y = now.getFullYear();
    return getTotals((transactions || []).filter(function (t) {
      var d = new Date(t.date + 'T00:00:00');
      return d.getMonth() === m && d.getFullYear() === y;
    }));
  }

  function getReportTotals(startDate, endDate) {
    var list = getUserTransactions().filter(function (t) {
      return t.date >= startDate && t.date <= endDate;
    });
    return $.extend({ count: list.length }, getTotals(list));
  }

  function getSavingsProgress() {
    var p = getProfile();
    if (!p) return 0;
    var goal = Number(p.savingsGoal || 0), total = Number(p.totalSavings || 0);
    if (goal <= 0) return 0;
    return Math.min(100, Math.max(0, (total / goal) * 100));
  }

  function getRecentTransactions(limit) {
    return getUserTransactions().slice().sort(function (a, b) {
      return new Date(b.date) - new Date(a.date) || Number(b.id) - Number(a.id);
    }).slice(0, limit || 5);
  }

  function getArticleOfDay() {
    var list = [
      { title: 'Review your spending weekly',   text: 'A short weekly review helps you catch unnecessary spending before it becomes a habit.' },
      { title: 'Save first, then spend',         text: 'Move a small amount to savings first. This makes your progress visible and consistent.' },
      { title: 'Use categories carefully',       text: 'When categories are clear and consistent, your reports become much more useful.' },
      { title: 'Check trends, not just totals',  text: 'Income and expenses are easier to manage when you compare periods instead of looking at one number.' }
    ];
    return list[new Date().getDate() % list.length];
  }

  function getKpi() {
    var transactions = getUserTransactions();
    var monthly = getMonthlyTotals(transactions);
    var ratio = monthly.income > 0 ? ((monthly.income - monthly.expenses) / monthly.income) * 100 : 0;
    var label = ratio >= 35 ? 'Strong' : ratio >= 15 ? 'Stable' : ratio >= 0 ? 'Watch closely' : 'Needs attention';
    return { value: ratio, label: label, message: 'How much of this month\'s income remains after expenses.' };
  }

  /* ── UI helpers ── */
  function showMessage(selector, type, message) {
    $(selector).html('<div class="alert alert-' + type + ' mb-0">' + message + '</div>');
  }

  function getStatusBadge(status) {
    return '<span class="badge badge-' + (status || '').toLowerCase() + '">' + (status || '') + '</span>';
  }

  function getInitials() {
    var cur = getCurrentUser();
    return cur ? (cur.username || cur.fullName || cur.email || 'U').charAt(0).toUpperCase() : 'U';
  }

  function transactionRow(item) {
    var isIncome = item.type === 'Income';
    return '<tr>' +
      '<td>' + item.date + '</td>' +
      '<td><span class="badge badge-' + (isIncome ? 'income' : 'expense') + '">' + item.type + '</span></td>' +
      '<td>' + item.category + '</td>' +
      '<td>' + item.description + '</td>' +
      '<td class="' + (isIncome ? 'amt-income' : 'amt-expense') + '">' + (isIncome ? '+' : '-') + formatAmount(item.amount) + '</td>' +
      '<td>' + getStatusBadge(item.status) + '</td>' +
      '<td class="text-end">' +
        '<a href="edit-transaction.html?id=' + item.id + '" class="btn btn-outline btn-sm me-2">Edit</a>' +
        '<button class="btn btn-danger-soft btn-sm delete-transaction" data-id="' + item.id + '">Delete</button>' +
      '</td>' +
      '</tr>';
  }

  function renderNav(active) {
    var cur      = getCurrentUser();
    var username = cur ? cur.username : 'user';
    var nav = ['transactions','savings','reports'].map(function (page) {
      var label = page.charAt(0).toUpperCase() + page.slice(1);
      return '<li><a class="nav-link' + (active === page ? ' active' : '') + '" href="' + page + '.html">' + label + '</a></li>';
    }).join('');

    $('#mainNavContainer').html(
      '<nav class="navbar-app">' +
        '<div class="frame">' +
          '<a class="brand" href="dashboard.html">Personal Finance System</a>' +
          '<ul class="nav-pills d-none d-md-flex">' + nav + '</ul>' +
          '<div class="dropdown" style="margin-left:auto;">' +
            '<button class="avatar-btn" data-bs-toggle="dropdown" aria-expanded="false">' + getInitials() + '</button>' +
            '<ul class="dropdown-menu dropdown-menu-end">' +
              '<li><span class="dropdown-item text-muted" style="font-size:.8rem;cursor:default;">@' + username + '</span></li>' +
              '<li><a class="dropdown-item" href="profile.html">Edit profile</a></li>' +
              '<li><button class="dropdown-item" id="logoutBtn">Log out</button></li>' +
            '</ul>' +
          '</div>' +
        '</div>' +
      '</nav>'
    );
  }

  function queryId() {
    return new URLSearchParams(window.location.search).get('id');
  }

  /* ── Demo seed ── */
  function seedDemoIfEmpty() {
    if (getUsers().length > 0) return;
    var demo = { fullName: 'Demo User', username: 'demo_user', email: 'demo@example.com', password: 'demo1234' };
    saveUsers([demo]);
    saveCurrentUser({ fullName: demo.fullName, username: demo.username, email: demo.email });
    saveProfile({ fullName: demo.fullName, username: demo.username, email: demo.email, phone: '0691234567', age: '25', occupation: 'Student', currency: 'EUR', savingsGoal: 1000, totalSavings: 250, createdAt: new Date().toISOString() });
    addTransaction({ date: new Date().toISOString().split('T')[0], type: 'Income',  category: 'Salary',    description: 'Part-time salary',  amount: 500, status: 'Completed' });
    addTransaction({ date: new Date().toISOString().split('T')[0], type: 'Expense', category: 'Groceries', description: 'Weekly groceries', amount:  60, status: 'Completed' });
    logout();
  }

  /* ── Init ── */
  $(function () {
    seedDemoIfEmpty();
    $(document).on('click', '#logoutBtn', function () {
      logout();
      window.location.href = 'authentication.html';
    });
    $(document).on('click', '.delete-transaction', function () {
      var id = Number($(this).data('id'));
      if (!confirm('Delete this transaction?')) return;
      deleteTransaction(id);
      window.location.reload();
    });
  });

  return {
    getUsers, saveUsers, getCurrentUser, saveCurrentUser, logout,
    getProfile, saveProfile,
    getTransactions, getUserTransactions, addTransaction, updateTransaction,
    getTransactionById, deleteTransaction,
    formatAmount, validateEmail, validateUsername, validatePassword,
    getValidationMessages, getCategories, fillCategoryOptions,
    requireLogin, getTotals, getMonthlyTotals, getReportTotals,
    getSavingsProgress, getRecentTransactions, getArticleOfDay, getKpi,
    showMessage, renderNav, queryId, transactionRow,
    convertAmount, convertUserFinancialData, getCurrencyMap, getStatusBadge
  };
})();
