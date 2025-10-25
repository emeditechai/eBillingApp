// Optimized Estimation JavaScript with SubCategory Support
$(document).ready(function() {
    let menuData = [];
    let allSubCategories = [];
    // Persist selected quantities across pagination/filtering
    const selectedQuantities = {}; // keyed by PLU (or name if PLU missing)
    
    // Initialize page
    loadMenuData();
    
    // Event handlers
    $('#menuItemSearch').on('input', debounce(filterItems, 300));
    $('#categoryFilter').on('change', onCategoryChange);
    $('#subCategoryFilter').on('change', filterItems);
    $('#estimateBtn').on('click', generateEstimate);
    $('#clearEstimateBtn').on('click', clearEstimate);
    $('#printEstimateBtn').on('click', printEstimate);
    $(document).on('change', '.qty-input', validateQuantity);
    
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }
    
    function loadMenuData() {
        showLoading();
        
        // Load menu data from the dedicated API endpoint with SubCategory support
        $.get('/Menu/GetMenuItemsForEstimation')
            .done(function(data) {
                if (data.error) {
                    showError('Failed to load menu items: ' + data.error);
                    return;
                }
                
                menuData = [];
                allSubCategories = [];
                
                data.forEach(function(item) {
                    const menuItem = {
                        plu: item.plu || '',
                        name: item.name || '',
                        category: item.category || 'Uncategorized',
                        subCategory: item.subCategory || '',
                        price: parseFloat(item.price) || 0,
                        categoryId: item.categoryId,
                        subCategoryId: item.subCategoryId,
                        isAvailable: item.isAvailable
                    };
                    
                    // Only include available items
                    if (menuItem.isAvailable) {
                    menuData.push(menuItem);

                    // initialize selectedQuantities entry for this item if missing
                    const key = menuItem.plu || menuItem.name;
                    if (!(key in selectedQuantities)) selectedQuantities[key] = 0;
                        
                        // Collect unique categories and subcategories
                        if (menuItem.subCategory && menuItem.subCategory !== '' && menuItem.subCategory !== '-') {
                            const existing = allSubCategories.find(s => s.name === menuItem.subCategory);
                            if (!existing) {
                                allSubCategories.push({
                                    name: menuItem.subCategory,
                                    category: menuItem.category
                                });
                            }
                        }
                    }
                });
                
                renderMenuItems(menuData);
                populateCategories();
                populateSubCategoriesForAll();
            })
            .fail(() => {
                showError('Failed to load menu items');
            });
    }
    
    function renderMenuItems(items) {
        const tbody = $('#menuItemsTableBody');
        tbody.empty();
        
        if (items.length === 0) {
            tbody.html('<tr><td colspan="6" class="text-center text-muted">No items found</td></tr>');
            return;
        }
        
        items.forEach(item => {
            const subCategoryDisplay = (item.subCategory && item.subCategory !== '' && item.subCategory !== '-')
                ? item.subCategory
                : '-';

            const key = item.plu || item.name;
            const currentQty = selectedQuantities[key] || 0;

            tbody.append(`
                <tr data-price="${item.price}" data-plu="${item.plu}">
                    <td>${item.name}</td>
                    <td>₹${item.price.toFixed(2)}</td>
                    <td>${item.category}</td>
                    <td>${subCategoryDisplay}</td>
                    <td>${item.plu}</td>
                    <td><input type="number" data-plu="${item.plu}" class="form-control form-control-sm qty-input" min="0" value="${currentQty}"></td>
                </tr>
            `);
        });
    }
    
    function populateCategories() {
        const categories = [...new Set(menuData.map(item => item.category))];
        const select = $('#categoryFilter');
        select.empty().append('<option value="all">All Categories</option>');
        categories.forEach(cat => select.append(`<option value="${cat.toLowerCase()}">${cat}</option>`));
    }
    
    function populateSubCategoriesForAll() {
        const select = $('#subCategoryFilter');
        select.empty().append('<option value="all">All SubCategories</option>');
        allSubCategories.forEach(sub => select.append(`<option value="${sub.name.toLowerCase()}" data-category="${sub.category.toLowerCase()}">${sub.name}</option>`));
    }
    
    function onCategoryChange() {
        const selectedCategory = $('#categoryFilter').val();
        const subSelect = $('#subCategoryFilter');
        
        // Reset subcategory filter
        subSelect.empty().append('<option value="all">All SubCategories</option>');
        
        if (selectedCategory === 'all') {
            // Show all subcategories
            allSubCategories.forEach(sub => {
                subSelect.append(`<option value="${sub.name.toLowerCase()}" data-category="${sub.category.toLowerCase()}">${sub.name}</option>`);
            });
        } else {
            // Show only subcategories for selected category
            const filteredSubs = allSubCategories.filter(sub => sub.category.toLowerCase() === selectedCategory);
            filteredSubs.forEach(sub => {
                subSelect.append(`<option value="${sub.name.toLowerCase()}" data-category="${sub.category.toLowerCase()}">${sub.name}</option>`);
            });
        }
        
        // Apply filtering
        filterItems();
    }
    
    function filterItems() {
        const search = $('#menuItemSearch').val().toLowerCase();
        const category = $('#categoryFilter').val();
        const subCategory = $('#subCategoryFilter').val();
        
        let filtered = menuData;
        
        // Filter by search text
        if (search) {
            filtered = filtered.filter(item => 
                item.name.toLowerCase().includes(search) || 
                item.plu.toLowerCase().includes(search)
            );
        }
        
        // Filter by category
        if (category !== 'all') {
            filtered = filtered.filter(item => item.category.toLowerCase() === category);
        }
        
        // Filter by subcategory
        if (subCategory !== 'all') {
            filtered = filtered.filter(item => item.subCategory.toLowerCase() === subCategory);
        }
        
        renderMenuItems(filtered);
    }
    
    function generateEstimate() {
        const items = [];
        let total = 0;
        
        // Build estimate from persisted quantities so selections on other pages are included
        for (const key in selectedQuantities) {
            const qty = parseInt(selectedQuantities[key]) || 0;
            if (qty > 0) {
                // Find the menu item by PLU first, fall back to name match
                const menuItem = menuData.find(mi => (mi.plu && mi.plu === key) || mi.name === key);
                if (!menuItem) continue;
                const name = menuItem.name;
                const price = parseFloat(menuItem.price) || 0;
                const subtotal = qty * price;
                items.push({name, price, qty, subtotal});
                total += subtotal;
            }
        }
        
        renderEstimation(items, total);
    }
    
    function renderEstimation(items, total) {
        const tbody = $('#estimationTableBody');
        tbody.empty();
        
        if (items.length === 0) {
            tbody.html('<tr><td colspan="4" class="text-center text-muted">No items selected</td></tr>');
            $('.estimation-section').hide();
            return;
        }
        
        items.forEach(item => {
            tbody.append(`
                <tr>
                    <td>${item.name}</td>
                    <td class="text-end">₹${item.price.toFixed(2)}</td>
                    <td class="text-center">${item.qty}</td>
                    <td class="text-end">₹${item.subtotal.toFixed(2)}</td>
                </tr>
            `);
        });
        
    // GST/Tax is not required for the estimation page — show zero to avoid confusion
    const tax = 0.00;
    const grand = total; // No tax applied on the estimation page
        
        tbody.append(`
            <tr class="border-top subtotal-row"><td colspan="3" class="text-end fw-semibold">Subtotal</td><td class="text-end fw-semibold">₹${total.toFixed(2)}</td></tr>
            <tr class="total-row"><td colspan="3" class="text-end fw-bold">Grand Total</td><td class="text-end fw-bold">₹${grand.toFixed(2)}</td></tr>
        `);

        // Update side summary card (no GST shown on estimation)
        $('#subtotalValue').text(`₹${total.toFixed(2)}`);
        $('#gstValue').text(`₹0.00`); // keep element for layout but show zero
        $('#grandTotalValue').text(`₹${grand.toFixed(2)}`);
        $('#estimateMeta').text(`${items.length} item(s) • Updated ${new Date().toLocaleTimeString()}`);
        
        $('.estimation-section').show();
        $('#printEstimateBtn, #clearEstimateBtn').show();
        showToast('Estimate generated successfully', 'success');
    }
    
    function clearEstimate() {
        $('.qty-input').val(0);
        $('#estimationTableBody').empty();
        $('.estimation-section').hide();
        showToast('Estimate cleared', 'info');
    }
    
    function printEstimate() {
        const date = new Date();
        const estimateNo = `EST-${date.getFullYear()}${(date.getMonth()+1).toString().padStart(2,'0')}${date.getDate().toString().padStart(2,'0')}-${Math.floor(Math.random()*1000)}`;
        const subtotal = $('#subtotalValue').text();
        const gst = $('#gstValue').text();
        const grand = $('#grandTotalValue').text();
        const meta = $('#estimateMeta').text();

        let rowsHtml = '';
        
        $('#estimationTableBody tr').each(function() {
            const tds = $(this).find('td');
            if (tds.length === 4) {
                rowsHtml += '<tr>';
                tds.each(function(i){
                    const cls = i === 1 || i === 3 ? 'text-right' : i === 2 ? 'text-center' : '';
                    rowsHtml += `<td class="${cls}">${$(this).text()}</td>`;
                });
                rowsHtml += '</tr>';
            }
        });

        const content = `<!DOCTYPE html><html><head><title>Order Estimate</title>
            <meta charset='utf-8' />
            <style>
                body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Arial,sans-serif;margin:28px;color:#222;}
                h1,h2,h3{margin:0;font-weight:600}
                .doc-header{text-align:center;margin-bottom:18px;padding-bottom:10px;border-bottom:3px solid #4b6cb7}
                table{width:100%;border-collapse:collapse;margin-top:10px;font-size:13px}
                th,td{border:1px solid #dcdfe3;padding:6px 8px}
                th{background:#f1f3f6;text-align:left;font-weight:600}
                .text-right{text-align:right}.text-center{text-align:center}
                .totals{margin-top:18px;max-width:280px;margin-left:auto;font-size:13px}
                .totals table{border:1px solid #dcdfe3}
                .totals td{border:0;padding:4px 0}
                .totals tr.divider td{border-top:1px solid #c7ccd1}
                .grand{font-size:15px;font-weight:700}
                .meta{font-size:11px;color:#555;margin-top:4px}
                .footer{text-align:center;margin-top:40px;font-style:italic;font-size:12px;color:#555}
                @media print { body{margin:8mm 10mm;} }
            </style>
        </head><body>
            <div class='doc-header'>
                <h2>Restaurant Management System</h2>
                <h3>ORDER ESTIMATE</h3>
                <div class='meta'>Estimate #: ${estimateNo} | Date: ${date.toLocaleDateString()} | ${meta}</div>
            </div>
            <table><thead><tr><th>Item</th><th class='text-right'>Unit Price</th><th class='text-center'>Qty</th><th class='text-right'>Amount</th></tr></thead><tbody>${rowsHtml}</tbody></table>
            <div class='totals'>
                <table style='width:100%;border-collapse:collapse;'>
                        <tr><td>Subtotal</td><td class='text-right'>${subtotal}</td></tr>
                        <tr class='divider grand'><td>Total</td><td class='text-right'>${grand}</td></tr>
                </table>
            </div>
            <div class='footer'>Thank you for choosing our restaurant!</div>
        </body></html>`;
        
        const win = window.open('', '_blank', 'width=900,height=650');
        win.document.write(content);
        win.document.close();
        win.focus();
        setTimeout(() => { win.print(); }, 400);
        showToast('Print ready', 'success');
    }
    
    function validateQuantity() {
        const $input = $(this);
        const val = parseInt($input.val());
        const normalized = (isNaN(val) || val < 0) ? 0 : val;
        $input.val(normalized);

        // persist quantity using PLU or name key
        const key = $input.data('plu') || $input.closest('tr').find('td:eq(0)').text();
        if (key) {
            selectedQuantities[key] = normalized;
        }
    }
    
    function showLoading() {
        $('#menuItemsTableBody').html('<tr><td colspan="5" class="text-center"><div class="loading-spinner mx-auto"></div></td></tr>');
    }
    
    function showError(msg) {
        $('#menuItemsTableBody').html(`<tr><td colspan="5" class="text-center text-danger">${msg}</td></tr>`);
    }
    
    function showToast(message, type = 'info') {
        $('.toast-notification').remove();
        const colors = {success: 'bg-success', warning: 'bg-warning', error: 'bg-danger', info: 'bg-info'};
        const toast = $(`<div class="toast-notification position-fixed top-0 end-0 m-3 ${colors[type]} text-white p-3 rounded shadow">${message}</div>`);
        $('body').append(toast);
        setTimeout(() => toast.fadeOut(() => toast.remove()), 3000);
    }
});