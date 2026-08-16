// Converts Quill's flat list markup into properly nested lists for storage.
//
// Quill does not nest lists. It emits a single flat <ol> where each <li> carries
// its bullet/number style in data-list and its depth in a ql-indent-N class:
//
//   <ol>
//     <li data-list="bullet">Top</li>
//     <li data-list="bullet" class="ql-indent-1">Nested</li>
//   </ol>
//
// Stored as-is that renders flat everywhere outside the editor, because nothing
// on the Details page or in the PDFs understands ql-indent-N. Rebuilding it as
// real <ul>/<ol> nesting means every consumer indents it natively.
window.normalizeQuillLists = function (html) {
    const temp = document.createElement('div');
    temp.innerHTML = html;

    temp.querySelectorAll('ol, ul').forEach(list => {
        // Only process top-level lists; nested ones are handled with their parent.
        if (list.closest('li')) return;

        const fragment = document.createDocumentFragment();
        const stack = []; // stack[n] = the list element currently open at depth n

        Array.from(list.children).forEach(li => {
            const type = li.getAttribute('data-list')
                || (list.tagName.toLowerCase() === 'ul' ? 'bullet' : 'ordered');
            const tag = type === 'bullet' ? 'ul' : 'ol';

            const match = (li.getAttribute('class') || '').match(/ql-indent-(\d+)/);
            let depth = match ? parseInt(match[1], 10) : 0;

            // A list cannot start deeper than one level below what is already open.
            depth = Math.min(depth, stack.length);

            // Close any levels deeper than this item.
            while (stack.length > depth + 1) stack.pop();

            // Open a new list if this depth is unused, or switches bullet/number.
            if (!stack[depth] || stack[depth].tagName.toLowerCase() !== tag) {
                const newList = document.createElement(tag);

                if (depth === 0) {
                    fragment.appendChild(newList);
                } else {
                    // Nest inside the last item of the level above, so the
                    // result is valid <li><ul>...</ul></li> markup.
                    const parentList = stack[depth - 1];
                    const lastLi = parentList.lastElementChild;
                    (lastLi || parentList).appendChild(newList);
                }

                stack[depth] = newList;
                stack.length = depth + 1;
            }

            const newLi = document.createElement('li');
            newLi.innerHTML = li.innerHTML;
            stack[depth].appendChild(newLi);
        });

        list.parentNode.insertBefore(fragment, list);
        list.remove();
    });

    return temp.innerHTML;
};
