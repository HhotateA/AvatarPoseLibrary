// Features カルーセルの自動スクロールを削除。CSSで2×3グリッド配置を行います。
const gitURL = 'https://api.github.com/repos/HhotateA/AvatarPoseLibrary';

window.addEventListener('DOMContentLoaded', () => {
  // ===== UnityPackageリンクの作成 =====
  const btn = document.querySelector('.unity-download');
  fetch(`${gitURL}/releases/latest`)
    .then(res => {
      if (!res.ok) throw new Error('failed to fetch release');
      return res.json();
    })
    .then(data => {
      const asset = data.assets.find(a => a.name.endsWith('.unitypackage'));
      if (asset) {
        btn.href = asset.browser_download_url;
        btn.title = `Download ${asset.name}`;
      }
    })
    .catch(err => console.error('最新リリースの取得に失敗しました:', err));

  // ===== Booth ギャラリーの自動スクロール設定 =====
  const boothCarousel = document.querySelector('.booth-gallery .carousel');
  const boothTrack = boothCarousel.querySelector('.carousel-track');
  let boothSpeed = 0.5;
  let boothScroll = 0;

  fetch('products.json')
    .then(res => {
      if (!res.ok) throw new Error('products.json の取得に失敗');
      return res.json();
    })
    .then(items => {
      // ランダムにシャッフルして先頭15アイテムを選択
      const shuffled = [...items].sort(() => 0.5 - Math.random());
      const selected = shuffled.slice(0, 15);
      // 同じ並びを複製して合計30アイテム
      const list = [...selected, ...selected];

      list.forEach(item => {
        const a = document.createElement('a');
        a.href = item.url;
        a.target = '_blank';
        a.className = 'gallery-item';

        const img = document.createElement('img');
        img.src = item.thumbnail;
        img.alt = item.title;
        a.appendChild(img);

        const title = document.createElement('div');
        title.className = 'title';
        title.textContent = item.title;
        a.appendChild(title);

        boothTrack.appendChild(a);
      });

      // 自動スクロール（1.5ページ分スクロールしたら0.5ページ分の位置へジャンプ）
      const pageWidth = boothCarousel.clientWidth;
      function stepBooth() {
        boothScroll += boothSpeed;
        if (boothScroll >= pageWidth * 1.5) {
          boothScroll = pageWidth * 0.5;
        }
        boothCarousel.scrollLeft = boothScroll;
        requestAnimationFrame(stepBooth);
      }
      requestAnimationFrame(stepBooth);

      // マウスホバーで一時停止／再開
      boothCarousel.addEventListener('mouseenter', () => boothSpeed = 0);
      boothCarousel.addEventListener('mouseleave', () => boothSpeed = 0.5);
    })
    .catch(err => {
      console.error(err);
      boothTrack.innerHTML = '<p>商品の読み込みに失敗しました。</p>';
    });
});