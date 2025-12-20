// Unified modern order details script
(function(){
  const root = document.getElementById('orderDetailsApp');
  if(!root) return;
  const orderId = root.getAttribute('data-order-id');
  const addForm = document.getElementById('orderItemAddForm');
  const addBtn = document.getElementById('addItemBtn');
  const menuInput = document.getElementById('menuItemInput');
  const qtyInput = document.getElementById('menuItemQty');
  const saveBtn = document.getElementById('saveOrderBtn');
  const tbody = document.getElementById('orderItemsBody');
  const subtotalCell = document.getElementById('orderSubtotalCell');
  const taxCell = document.getElementById('orderTaxCell');
  const totalCell = document.getElementById('orderTotalCell');
  const subtotalCellSide = document.getElementById('orderSubtotalCellSide');
  const taxCellSide = document.getElementById('orderTaxCellSide');
  const totalCellSide = document.getElementById('orderTotalCellSide');
  const subtotalCellMobile = document.getElementById('orderSubtotalCellMobile');
  const totalCellMobile = document.getElementById('orderTotalCellMobile');
  const selectAllFire = document.getElementById('selectAllFire');
  let tempIdCounter = -1;

  function setTotalsUI(subtotal, tax, total){
    if(subtotalCell) subtotalCell.textContent = formatMoney(subtotal);
    if(taxCell) taxCell.textContent = formatMoney(tax);
    if(totalCell) totalCell.textContent = formatMoney(total);
    if(subtotalCellSide) subtotalCellSide.textContent = formatMoney(subtotal);
    if(taxCellSide) taxCellSide.textContent = formatMoney(tax);
    if(totalCellSide) totalCellSide.textContent = formatMoney(total);
    if(subtotalCellMobile) subtotalCellMobile.textContent = formatMoney(subtotal);
    if(totalCellMobile) totalCellMobile.textContent = formatMoney(total);
  }

  async function refreshTotalsFromServer(){
    try{
      const res = await fetch(`/Order/GetOrderTotalsJson?orderId=${encodeURIComponent(orderId)}`);
      const data = await res.json().catch(()=>null);
      if(!res.ok || !data || !data.success){
        return;
      }
      const subtotal = Number(data.subtotal ?? 0);
      const tax = Number(data.taxAmount ?? 0);
      const total = Number(data.totalAmount ?? (subtotal + tax));
      setTotalsUI(subtotal, tax, total);
    }catch{
      // non-fatal
    }
  }

  // Nice confirm modal helper (falls back to window.confirm)
  async function showConfirm(message){
    const modalEl = document.getElementById('confirmModal');
    // Preferred: Bootstrap modal if available
    if(modalEl && window.bootstrap && bootstrap.Modal){
      return new Promise((resolve)=>{
        const msgEl = document.getElementById('confirmModalMessage');
        if(msgEl) msgEl.textContent = message || 'Are you sure?';
        const yesBtn = document.getElementById('confirmModalYesBtn');
        const modal = new bootstrap.Modal(modalEl);
        const cleanup = ()=>{
          yesBtn?.removeEventListener('click', onYes);
          modalEl.removeEventListener('hidden.bs.modal', onHide);
        };
        const onYes = ()=>{ cleanup(); modal.hide(); resolve(true); };
        const onHide = ()=>{ cleanup(); resolve(false); };
        yesBtn?.addEventListener('click', onYes);
        modalEl.addEventListener('hidden.bs.modal', onHide, { once: true });
        modal.show();
      });
    }
    // Fallback: custom lightweight modal (no browser alert)
    return new Promise((resolve)=>{
      // Create overlay only once
      let overlay = document.getElementById('simpleConfirmOverlay');
      if(!overlay){
        overlay = document.createElement('div');
        overlay.id = 'simpleConfirmOverlay';
        overlay.innerHTML = `
          <div class="scf-backdrop"></div>
          <div class="scf-dialog">
            <div class="scf-header">Confirm</div>
            <div class="scf-body"><span id="scfMessage"></span></div>
            <div class="scf-footer">
              <button id="scfNo" class="btn btn-sm btn-secondary">No</button>
              <button id="scfYes" class="btn btn-sm btn-danger">Yes</button>
            </div>
          </div>`;
        document.body.appendChild(overlay);
        // Basic styles
        const style = document.createElement('style');
        style.id = 'simpleConfirmStyles';
        style.textContent = `
          #simpleConfirmOverlay{position:fixed;inset:0;z-index:2050;display:flex;align-items:center;justify-content:center}
          #simpleConfirmOverlay .scf-backdrop{position:absolute;inset:0;background:rgba(0,0,0,0.45)}
          #simpleConfirmOverlay .scf-dialog{position:relative;background:#fff;min-width:280px;max-width:90vw;border-radius:8px;box-shadow:0 6px 24px rgba(0,0,0,.2);overflow:hidden}
          #simpleConfirmOverlay .scf-header{font-weight:600;padding:.5rem .75rem;border-bottom:1px solid #e5e7eb}
          #simpleConfirmOverlay .scf-body{padding: .75rem .75rem}
          #simpleConfirmOverlay .scf-footer{display:flex;gap:.5rem;justify-content:flex-end;padding:.5rem .75rem;border-top:1px solid #e5e7eb}
        `;
        document.head.appendChild(style);
      }
      overlay.querySelector('#scfMessage').textContent = message || 'Are you sure?';
      overlay.style.display = 'flex';
      const yes = overlay.querySelector('#scfYes');
      const no = overlay.querySelector('#scfNo');
      const close = (val)=>{ overlay.style.display = 'none'; yes.removeEventListener('click', onYes); no.removeEventListener('click', onNo); resolve(val); };
      const onYes = ()=> close(true);
      const onNo = ()=> close(false);
      yes.addEventListener('click', onYes);
      no.addEventListener('click', onNo);
    });
  }

  function disablePaymentButton(){
    const paymentBtn = document.querySelector('a[href*="/Payment/Index/"]');
    if(!paymentBtn) return;
    paymentBtn.classList.add('btn-disabled');
    paymentBtn.setAttribute('aria-disabled','true');
    paymentBtn.style.pointerEvents = 'none';
  }

  function enablePaymentButtonIfAllowed(){
    const paymentBtn = document.querySelector('a[href*="/Payment/Index/"]');
    if(!paymentBtn) return;
    if(root.getAttribute('data-is-fully-paid') === 'true') return;
    paymentBtn.classList.remove('btn-disabled');
    paymentBtn.removeAttribute('aria-disabled');
    paymentBtn.style.pointerEvents = '';
  }

  function setFireEnabled(enabled){
    const fireBtn = document.getElementById('fireItemsBtn');
    if(!fireBtn) return;
    if(enabled){
      fireBtn.classList.remove('disabled');
      fireBtn.removeAttribute('disabled');
      fireBtn.style.pointerEvents = '';
    } else {
      fireBtn.classList.add('disabled');
      fireBtn.setAttribute('disabled','disabled');
      fireBtn.style.pointerEvents = 'none';
    }
  }

  function hasPendingUndoDelete(){
    return !!tbody.querySelector('tr.pending-delete');
  }

  function getAntiForgery(){
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
  }

  function parsePrice(text){
    if(!text) return 0; return parseFloat(text.replace(/[^0-9.]/g,''))||0;
  }

  function formatMoney(v){
    return '₹'+v.toFixed(2);
  }

  function recalcTotals(){
    let subtotal = 0;
    tbody.querySelectorAll('tr').forEach(tr=>{
      if(tr.classList.contains('cancelled-row') || tr.getAttribute('data-cancelled') === 'true') return;
      if(tr.classList.contains('pending-delete')) return;
      const subCell = tr.querySelector('.subtotal-cell');
      if(subCell){
        subtotal += parsePrice(subCell.textContent);
      }
    });
    if(subtotalCell) subtotalCell.textContent = formatMoney(subtotal);
    if(subtotalCellSide) subtotalCellSide.textContent = formatMoney(subtotal);
    if(subtotalCellMobile) subtotalCellMobile.textContent = formatMoney(subtotal);
    // tax + total: keep existing tax value; recompute total (subtotal + tax for now)
    const tax = taxCell ? parsePrice(taxCell.textContent) : (taxCellSide ? parsePrice(taxCellSide.textContent) : 0);
    if(taxCellSide && taxCell) taxCellSide.textContent = taxCell.textContent;
    if(totalCell) totalCell.textContent = formatMoney(subtotal + tax);
    if(totalCellSide) totalCellSide.textContent = formatMoney(subtotal + tax);
    if(totalCellMobile) totalCellMobile.textContent = formatMoney(subtotal + tax);
  }

  // Undo toast (5–10s) for safe delete
  function showUndoToast(message, seconds, onUndo, onExpire){
    const timeoutMs = Math.max(5000, Math.min(10000, (seconds||7) * 1000));
    let container = document.getElementById('orderUndoToastContainer');
    if(!container){
      container = document.createElement('div');
      container.id = 'orderUndoToastContainer';
      container.style.position = 'fixed';
      container.style.right = '16px';
      container.style.bottom = '16px';
      container.style.zIndex = '2060';
      container.style.display = 'flex';
      container.style.flexDirection = 'column';
      container.style.gap = '8px';
      document.body.appendChild(container);
    }
    const toastEl = document.createElement('div');
    toastEl.className = 'alert alert-dark shadow-sm mb-0';
    toastEl.style.minWidth = '280px';
    toastEl.style.display = 'flex';
    toastEl.style.alignItems = 'center';
    toastEl.style.justifyContent = 'space-between';
    toastEl.style.gap = '12px';
    toastEl.innerHTML = `
      <div style="font-size:0.9rem;">${message || 'Item removed'}</div>
      <button type="button" class="btn btn-sm btn-outline-light">Undo</button>
    `;
    const undoBtn = toastEl.querySelector('button');
    container.appendChild(toastEl);

    let done = false;
    const cleanup = ()=>{
      if(done) return;
      done = true;
      try{ undoBtn.removeEventListener('click', onUndoClick); }catch{}
      try{ toastEl.remove(); }catch{}
    };
    const onUndoClick = ()=>{
      cleanup();
      try{ onUndo && onUndo(); }catch{}
    };
    undoBtn.addEventListener('click', onUndoClick);

    const t = setTimeout(()=>{
      cleanup();
      try{ onExpire && onExpire(); }catch{}
    }, timeoutMs);

    // If the toast is removed externally, ensure timer doesn’t leak
    const mo = new MutationObserver(()=>{
      if(!document.body.contains(toastEl)){
        clearTimeout(t);
        mo.disconnect();
      }
    });
    mo.observe(document.body, { childList: true, subtree: true });
  }

  function buildExistingRowPayload(tr){
    return {
      OrderItemId: parseInt(tr.getAttribute('data-order-item-id'),10),
      Quantity: parseInt(tr.querySelector('.item-qty').value,10) || 1,
      SpecialInstructions: tr.querySelector('.item-note').value.trim(),
      IsNew: false,
      MenuItemId: null,
      TempId: null
    };
  }

  function buildNewRowPayload(tr){
    return {
      OrderItemId: 0,
      Quantity: parseInt(tr.querySelector('.item-qty').value,10) || 1,
      SpecialInstructions: tr.querySelector('.item-note').value.trim(),
      IsNew: true,
      MenuItemId: parseInt(tr.getAttribute('data-menu-item-id'),10),
      TempId: parseInt(tr.getAttribute('data-temp-id'),10)
    };
  }

  function collectPayload(){
    const rows = Array.from(tbody.querySelectorAll('tr'));
    return rows.map(r => r.classList.contains('existing-item-row') ? buildExistingRowPayload(r) : buildNewRowPayload(r));
  }

  function updateRowSubtotal(tr){
    const qtyEl = tr.querySelector('.item-qty');
    const qty = parseInt(qtyEl.value,10)||1;
    const unitText = tr.querySelector('td:nth-child(5)')?.textContent || tr.querySelector('.unit-price')?.textContent;
    const unit = parsePrice(unitText);
    const subCell = tr.querySelector('.subtotal-cell');
    if(subCell){
      subCell.textContent = formatMoney(qty*unit);
    }
  }

  function attachRowEvents(tr){
    const qtyInput = tr.querySelector('.item-qty');
    if(qtyInput){
      qtyInput.addEventListener('change',()=>{updateRowSubtotal(tr); recalcTotals();});
    }
    const removeBtn = tr.querySelector('.remove-existing, .remove-new');
    if(removeBtn){
      removeBtn.addEventListener('click', async ()=>{
        // Safe delete:
        // - New rows: immediate hide + Undo; expire removes row.
        // - Existing rows (status=0 only in UI): hide + Undo; expire calls server cancel.
        if(tr.classList.contains('pending-delete')) return;

        if(tr.classList.contains('existing-item-row')){
          const id = parseInt(tr.getAttribute('data-order-item-id'),10);
          const status = parseInt(tr.getAttribute('data-item-status') || '0', 10) || 0;
          if(!id){ tr.remove(); recalcTotals(); return; }

          // Defensive: if somehow fired/printed item shows a delete button, require confirm.
          if(status > 0 && status !== 5){
            const ok = await showConfirm('This item appears already fired. Cancel anyway?');
            if(!ok) return;
          }

          tr.classList.add('pending-delete');
          tr.style.opacity = '0.4';
          tr.style.display = 'none';
          recalcTotals();
          disablePaymentButton();
          setFireEnabled(false);

          showUndoToast('Item removed. Undo?', 7,
            ()=>{
              tr.classList.remove('pending-delete');
              tr.style.opacity = '';
              tr.style.display = '';
              recalcTotals();
              enablePaymentButtonIfAllowed();
              if(!hasPendingUndoDelete()) setFireEnabled(true);
            },
            async ()=>{
              try{
                const token = (document.querySelector('input[name="__RequestVerificationToken"]').value)||'';
                const res = await fetch('/Order/CancelItem',{
                  method:'POST',
                  headers:{ 'Content-Type':'application/x-www-form-urlencoded; charset=UTF-8', 'RequestVerificationToken': token },
                  body:`orderItemId=${encodeURIComponent(id)}`
                });
                const data = await res.json().catch(()=>({success:false,message:'Invalid response'}));
                if(!res.ok || !data.success){ throw new Error(data.message||'Cancel failed'); }

                // Remove row without triggering "unsaved" state in the inline script
                root.dataset.skipDirty = 'true';
                tr.remove();

                await refreshTotalsFromServer();
                enablePaymentButtonIfAllowed();
                if(!hasPendingUndoDelete()) setFireEnabled(true);
                if(window.toastr){ toastr.success('Item cancelled'); }
              }catch(err){
                // Revert row on failure
                tr.classList.remove('pending-delete');
                tr.style.opacity = '';
                tr.style.display = '';
                recalcTotals();
                enablePaymentButtonIfAllowed();
                if(!hasPendingUndoDelete()) setFireEnabled(true);
                if(window.toastr){ toastr.error(err.message||'Cancel failed'); }
              }
            }
          );
        } else {
          tr.classList.add('pending-delete');
          tr.style.opacity = '0.4';
          tr.style.display = 'none';
          recalcTotals();
          disablePaymentButton();
          setFireEnabled(false);

          showUndoToast('Item removed. Undo?', 7,
            ()=>{
              tr.classList.remove('pending-delete');
              tr.style.opacity = '';
              tr.style.display = '';
              recalcTotals();
              enablePaymentButtonIfAllowed();
              if(!hasPendingUndoDelete()) setFireEnabled(true);
            },
            ()=>{
              tr.remove();
              recalcTotals();
              enablePaymentButtonIfAllowed();
              if(!hasPendingUndoDelete()) setFireEnabled(true);
            }
          );
        }
      });
    }
    const editBtn = tr.querySelector('.edit-row');
    if(editBtn){
      const noteInput = tr.querySelector('.item-note');
      const qty = tr.querySelector('.item-qty');
      const saveBtn = tr.querySelector('.save-row');
      const cancelBtn = tr.querySelector('.cancel-row');
      let original = { qty: qty?.value, note: noteInput?.value };
      editBtn.addEventListener('click', ()=>{
        original = { qty: qty.value, note: noteInput.value };
        qty.disabled = false; noteInput.disabled = false;
        editBtn.classList.add('d-none');
        saveBtn.classList.remove('d-none');
        cancelBtn.classList.remove('d-none');
        qty.focus();
      });
      saveBtn?.addEventListener('click', ()=>{
        qty.disabled = true; noteInput.disabled = true;
        saveBtn.classList.add('d-none');
        cancelBtn.classList.add('d-none');
        editBtn.classList.remove('d-none');
        // Mark row dirty implicitly by recalculating subtotal to ensure payload built
        updateRowSubtotal(tr); recalcTotals();
      });
      cancelBtn?.addEventListener('click', ()=>{
        qty.value = original.qty; noteInput.value = original.note;
        qty.disabled = true; noteInput.disabled = true;
        saveBtn.classList.add('d-none');
        cancelBtn.classList.add('d-none');
        editBtn.classList.remove('d-none');
        updateRowSubtotal(tr); recalcTotals();
      });
    }
  }

  function resolveMenuItemByName(name){
    const options = document.querySelectorAll('#menuItems option');
    const target = name.trim().toLowerCase();
    for(const opt of options){
      const optName = (opt.value||'').trim().toLowerCase();
      const optPlu = (opt.getAttribute('data-plu')||'').trim().toLowerCase();
      if(optName === target || optPlu === target){
        return {
          id: parseInt(opt.getAttribute('data-id'),10),
          price: parseFloat(opt.getAttribute('data-price')),
          displayName: opt.getAttribute('data-plu') && optPlu === target ? opt.textContent.split(' - ').slice(1).join(' - ').trim() : opt.value
        };
      }
    }
    return null;
  }

  function addNewItem(name, qty){
    const resolved = resolveMenuItemByName(name);
    if(!resolved){
      toast('Menu item not found','error');
      return;
    }
    if(qty < 1) qty = 1;
    const tr = document.createElement('tr');
    tr.className = 'new-item-row table-primary';
    tr.setAttribute('data-menu-item-id', resolved.id);
    tr.setAttribute('data-temp-id', tempIdCounter--);
    const subtotal = resolved.price * qty;
    tr.innerHTML = `
      <td class="text-center"><input type="checkbox" class="fire-select" disabled /></td>
      <td><div class="fw-semibold">${resolved.displayName || name}</div><input type="text" class="form-control form-control-sm item-note mt-1" placeholder="Note" /></td>
      <td class="text-center"><input type="number" class="form-control form-control-sm item-qty" value="${qty}" min="1" /></td>
      <td class="text-end">${formatMoney(resolved.price)}</td>
      <td class="text-end subtotal-cell">${formatMoney(subtotal)}</td>
      <td><span class="badge bg-info text-dark">New</span></td>
      <td class="text-end"><button type="button" class="btn btn-outline-danger btn-sm remove-new" aria-label="Remove"><i class="fas fa-times"></i></button></td>`;
    tbody.appendChild(tr);
    attachRowEvents(tr);
    recalcTotals();
  }

  function toast(msg,type){
    if(window.toastr){
      if(type==='error') toastr.error(msg); else if(type==='success') toastr.success(msg); else toastr.info(msg);
    } else {
      console[type==='error'?'error':'log']('[Toast]',msg);
    }
  }

  if(addForm){
    addForm.addEventListener('submit', e=>{
      e.preventDefault();
      const name = menuInput.value.trim();
      const qty = parseInt(qtyInput.value,10)||1;
      if(!name){toast('Enter a menu item','error');return;}
      addNewItem(name, qty);
      menuInput.value=''; qtyInput.value='1'; menuInput.focus();
    });
  }

  if(selectAllFire){
    selectAllFire.addEventListener('change', ()=>{
      const checked = selectAllFire.checked;
      tbody.querySelectorAll('.fire-select:not(:disabled)').forEach(cb=> cb.checked = checked);
    });
  }

  // Fire modal select all
  document.addEventListener('DOMContentLoaded', ()=>{
    const fireSelectAll = document.getElementById('fireSelectAll');
    if(fireSelectAll){
      fireSelectAll.addEventListener('change', ()=>{
        const checked = fireSelectAll.checked;
        document.querySelectorAll('.fire-item-checkbox').forEach(cb=> cb.checked = checked);
      });
    }
  });

  async function saveOrder(){
    const payload = collectPayload();
    console.log('Save order called. Payload:', payload);
    console.log('Order ID:', orderId);
    
    if(!payload.length){ 
      toast('No items to save','error'); 
      console.log('No items in payload');
      return; 
    }
    
    saveBtn.disabled = true; 
    const original = saveBtn.innerHTML; 
    saveBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Saving...';
    
    try{
      console.log('Making fetch request to:', `/Order/UpdateMultipleOrderItems?orderId=${orderId}`);
      const antiForgery = getAntiForgery();
      if(!antiForgery){
        console.warn('Anti-forgery token missing');
      }
      const res = await fetch(`/Order/UpdateMultipleOrderItems?orderId=${orderId}`,{
        method:'POST',
        headers: {
          'Content-Type':'application/json',
          'RequestVerificationToken': antiForgery
        },
        body: JSON.stringify(payload)
      });
      
      console.log('Response status:', res.status);
      console.log('Response headers:', res.headers);
      
      if (!res.ok) {
        throw new Error(`HTTP error! status: ${res.status}`);
      }
      
      const data = await res.json().catch((parseError)=>{
        console.error('JSON parse error:', parseError);
        return {success:false,message:'Invalid server response - could not parse JSON'};
      });
      
      console.log('Response data:', data);
      
      if(!data.success){ 
        throw new Error(data.message||'Save failed'); 
      }
      
      toast('Order saved','success');
      // Reload to get authoritative data (ensures IDs for new rows)
      setTimeout(()=>{ window.location.reload(); }, 600);
    }catch(err){
      console.error('Save order error:', err);
      toast(err.message||'Error saving order','error');
      saveBtn.disabled=false; 
      saveBtn.innerHTML = original;
    }
  }

  if(saveBtn){
    saveBtn.addEventListener('click', saveOrder);
  }

  // Attach events for existing rows
  tbody.querySelectorAll('tr').forEach(tr=> attachRowEvents(tr));

  // Data integrity: warn on accidental navigation when edits exist
  window.addEventListener('beforeunload', function(e){
    // Consider the page “dirty” if Save is enabled (meaning edits exist)
    const saveBtnEl = document.getElementById('saveOrderBtn');
    const isDirty = saveBtnEl && saveBtnEl.disabled === false;
    if(!isDirty) return;
    e.preventDefault();
    e.returnValue = '';
  });
})();
