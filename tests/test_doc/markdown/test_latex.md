# LaTeX 测试文档

欢迎使用 WeaveDoc Markdown 编辑器！

## 行内公式

行内公式使用 `$...$` 语法：

这是一个行内公式：$E = mc^2$，用于描述质能等价。

勾股定理：$a^2 + b^2 = c^2$

欧拉公式：$e^{i\pi} + 1 = 0$

## 块级公式

块级公式使用 `$$...$$` 语法：

$$
\int_{-\infty}^{\infty} e^{-x^2} dx = \sqrt{\pi}
$$

$$
\sum_{i=1}^{n} i = \frac{n(n+1)}{2}
$$

$$
\vec{F} = m\vec{a}
$$

## 复杂公式示例

矩阵表示：

$$
\begin{bmatrix}
1 & 2 & 3 \\
4 & 5 & 6 \\
7 & 8 & 9
\end{bmatrix}
$$

分段函数：

$$
f(x) =
\begin{cases}
x & \text{if } x \geq 0 \\
-x & \text{if } x < 0
\end{cases}
$$

## 常用数学符号

- 希腊字母：$\alpha, \beta, \gamma, \delta, \epsilon, \theta, \lambda, \mu, \pi, \sigma, \omega$
- 上标：$x^2$, $y^{10}$, $z'$
- 下标：$x_1$, $y_{i+1}$, $a_{ij}$
- 分数：$\frac{1}{2}$, $\frac{a}{b}$, $\frac{x+1}{y-1}$
- 根号：$\sqrt{x}$, $\sqrt[3]{8}$, $\sqrt{a+b}$
- 求和：$\sum_{i=1}^{n} x_i$
- 积分：$\int_{0}^{1} f(x) dx$
- 极限：$\lim_{x \to \infty} \frac{1}{x}$

## 注意事项

1. 行内公式 `$...$` 应该在同一行内完成
2. 块级公式 `$$...$$` 可以跨越多行
3. 避免在公式中使用未转义的 `$` 符号

祝您使用愉快！