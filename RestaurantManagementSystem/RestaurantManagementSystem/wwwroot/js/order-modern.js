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
  const selectAllFire = document.getElementById('selectAllFire');
  let tempIdCounter = -1;

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
      const subCell = tr.querySelector('.subtotal-cell');
      if(subCell){
        subtotal += parsePrice(subCell.textContent);
      }
    });
    subtotalCell.textContent = formatMoney(subtotal);
    // tax + total: keep existing tax value; recompute total (subtotal + tax for now)
    const tax = parsePrice(taxCell.textContent);
    totalCell.textContent = formatMoney(subtotal + tax);
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
        // For new rows just remove; for existing, call cancel API
        if(tr.classList.contains('existing-item-row')){
          const id = parseInt(tr.getAttribute('data-order-item-id'),10);
          if(!id) { tr.remove(); recalcTotals(); return; }
          const ok = await showConfirm('Cancel this item?');
          if(!ok) return;
          const originalHtml = removeBtn.innerHTML; 
          removeBtn.disabled = true; removeBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';
          try{
            const token = (document.querySelector('input[name="__RequestVerificationToken"]').value)||'';
            const res = await fetch('/Order/CancelItem',{
              method:'POST',
              headers:{ 'Content-Type':'application/x-www-form-urlencoded; charset=UTF-8', 'RequestVerificationToken': token },
              body:`orderItemId=${encodeURIComponent(id)}`
            });
            const data = await res.json().catch(()=>({success:false,message:'Invalid response'}));
            if(!res.ok || !data.success){ throw new Error(data.message||'Cancel failed'); }
            // Mark cancelled and remove the row (billing excludes cancelled items)
            tr.setAttribute('data-cancelled','true');
            const subCell = tr.querySelector('.subtotal-cell');
            if(subCell){ subCell.textContent = formatMoney(0); }
            setTimeout(()=>{ tr.remove(); recalcTotals(); }, 150);
            if(window.toastr){ toastr.success('Item cancelled'); }
            // Mark page dirty: enable save, disable payment until saved
            const saveBtnEl = document.getElementById('saveOrderBtn');
            if(saveBtnEl){ saveBtnEl.disabled = false; }
            disablePaymentButton();
          }catch(err){
            if(window.toastr){ toastr.error(err.message||'Cancel failed'); }
            removeBtn.disabled = false; removeBtn.innerHTML = originalHtml;
            return;
          }
        } else {
          tr.remove(); recalcTotals();
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
})();
