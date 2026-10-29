import { createRouter, createWebHashHistory } from 'vue-router';

const routes = [
  {
    path: '/',
    redirect: '/home',
  },
  {
    path: '/home',
    name: 'Home',
    component: () => import('@/views/Home.vue'),
  },
];

const router = createRouter({
  history: createWebHashHistory(), // WebView2 推荐使用 hash 模式
  routes,
});

// 挂载路由 API 到 window，供 C# 调用
if (typeof window !== 'undefined') {
  window.router = {
    /**
     * 导航到指定路由
     * @param {string} path - 路由路径，如 '/settings'
     */
    push(path) {
      router.push(path);
    },

    /**
     * 替换当前路由
     * @param {string} path - 路由路径
     */
    replace(path) {
      router.replace(path);
    },

    /**
     * 后退
     */
    back() {
      router.back();
    },

    /**
     * 获取当前路由路径
     * @returns {string}
     */
    current() {
      return router.currentRoute.value.path;
    },
  };
}

export default router;
