/* Chartist.js 0.11.4
 * Copyright © 2019 Gion Kunz
 * Free to use under either the WTFPL license or the MIT license.
 * https://raw.githubusercontent.com/gionkunz/chartist-js/master/LICENSE-WTFPL
 * https://raw.githubusercontent.com/gionkunz/chartist-js/master/LICENSE-MIT
 */

!function (e, t) { "function" == typeof define && define.amd ? define("Chartist", [], (function () { return e.Chartist = t() })) : "object" == typeof module && module.exports ? module.exports = t() : e.Chartist = t() }(this, (function () { var e = { version: "0.11.4" }; return function (e, t) { "use strict"; var i = e.window, n = e.document; t.namespaces = { svg: "http://www.w3.org/2000/svg", xmlns: "http://www.w3.org/2000/xmlns/", xhtml: "http://www.w3.org/1999/xhtml", xlink: "http://www.w3.org/1999/xlink", ct: "http://gionkunz.github.com/chartist-js/ct" }, t.noop = function (e) { return e }, t.alphaNumerate = function (e) { return String.fromCharCode(97 + e % 26) }, t.extend = function (e) { var i, n, s, r; for (e = e || {}, i = 1; i < arguments.length; i++)for (var a in n = arguments[i], r = Object.getPrototypeOf(e), n) "__proto__" === a || "constructor" === a || null !== r && a in r || (s = n[a], e[a] = "object" != typeof s || null === s || s instanceof Array ? s : t.extend(e[a], s)); return e }, t.replaceAll = function (e, t, i) { return e.replace(new RegExp(t, "g"), i) }, t.ensureUnit = function (e, t) { return "number" == typeof e && (e += t), e }, t.quantity = function (e) { if ("string" == typeof e) { var t = /^(\d+)\s*(.*)$/g.exec(e); return { value: +t[1], unit: t[2] || void 0 } } return { value: e } }, t.querySelector = function (e) { return e instanceof Node ? e : n.querySelector(e) }, t.times = function (e) { return Array.apply(null, new Array(e)) }, t.sum = function (e, t) { return e + (t || 0) }, t.mapMultiply = function (e) { return function (t) { return t * e } }, t.mapAdd = function (e) { return function (t) { return t + e } }, t.serialMap = function (e, i) { var n = [], s = Math.max.apply(null, e.map((function (e) { return e.length }))); return t.times(s).forEach((function (t, s) { var r = e.map((function (e) { return e[s] })); n[s] = i.apply(null, r) })), n }, t.roundWithPrecision = function (e, i) { var n = Math.pow(10, i || t.precision); return Math.round(e * n) / n }, t.precision = 8, t.escapingMap = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }, t.serialize = function (e) { return null == e ? e : ("number" == typeof e ? e = "" + e : "object" == typeof e && (e = JSON.stringify({ data: e })), Object.keys(t.escapingMap).reduce((function (e, i) { return t.replaceAll(e, i, t.escapingMap[i]) }), e)) }, t.deserialize = function (e) { if ("string" != typeof e) return e; e = Object.keys(t.escapingMap).reduce((function (e, i) { return t.replaceAll(e, t.escapingMap[i], i) }), e); try { e = void 0 !== (e = JSON.parse(e)).data ? e.data : e } catch (e) { } return e }, t.createSvg = function (e, i, n, s) { var r; return i = i || "100%", n = n || "100%", Array.prototype.slice.call(e.querySelectorAll("svg")).filter((function (e) { return e.getAttributeNS(t.namespaces.xmlns, "ct") })).forEach((function (t) { e.removeChild(t) })), (r = new t.Svg("svg").attr({ width: i, height: n }).addClass(s))._node.style.width = i, r._node.style.height = n, e.appendChild(r._node), r }, t.normalizeData = function (e, i, n) { var s, r = { raw: e, normalized: {} }; return r.normalized.series = t.getDataArray({ series: e.series || [] }, i, n), s = r.normalized.series.every((function (e) { return e instanceof Array })) ? Math.max.apply(null, r.normalized.series.map((function (e) { return e.length }))) : r.normalized.series.length, r.normalized.labels = (e.labels || []).slice(), Array.prototype.push.apply(r.normalized.labels, t.times(Math.max(0, s - r.normalized.labels.length)).map((function () { return "" }))), i && t.reverseData(r.normalized), r }, t.safeHasProperty = function (e, t) { return null !== e && "object" == typeof e && e.hasOwnProperty(t) }, t.isDataHoleValue = function (e) { return null == e || "number" == typeof e && isNaN(e) }, t.reverseData = function (e) { e.labels.reverse(), e.series.reverse(); for (var t = 0; t < e.series.length; t++)"object" == typeof e.series[t] && void 0 !== e.series[t].data ? e.series[t].data.reverse() : e.series[t] instanceof Array && e.series[t].reverse() }, t.getDataArray = function (e, i, n) { return e.series.map((function e(i) { if (t.safeHasProperty(i, "value")) return e(i.value); if (t.safeHasProperty(i, "data")) return e(i.data); if (i instanceof Array) return i.map(e); if (!t.isDataHoleValue(i)) { if (n) { var s = {}; return "string" == typeof n ? s[n] = t.getNumberOrUndefined(i) : s.y = t.getNumberOrUndefined(i), s.x = i.hasOwnProperty("x") ? t.getNumberOrUndefined(i.x) : s.x, s.y = i.hasOwnProperty("y") ? t.getNumberOrUndefined(i.y) : s.y, s } return t.getNumberOrUndefined(i) } })) }, t.normalizePadding = function (e, t) { return t = t || 0, "number" == typeof e ? { top: e, right: e, bottom: e, left: e } : { top: "number" == typeof e.top ? e.top : t, right: "number" == typeof e.right ? e.right : t, bottom: "number" == typeof e.bottom ? e.bottom : t, left: "number" == typeof e.left ? e.left : t } }, t.getMetaData = function (e, t) { var i = e.data ? e.data[t] : e[t]; return i ? i.meta : void 0 }, t.orderOfMagnitude = function (e) { return Math.floor(Math.log(Math.abs(e)) / Math.LN10) }, t.projectLength = function (e, t, i) { return t / i.range * e }, t.getAvailableHeight = function (e, i) { return Math.max((t.quantity(i.height).value || e.height()) - (i.chartPadding.top + i.chartPadding.bottom) - i.axisX.offset, 0) }, t.getHighLow = function (e, i, n) { var s = { high: void 0 === (i = t.extend({}, i, n ? i["axis" + n.toUpperCase()] : {})).high ? -Number.MAX_VALUE : +i.high, low: void 0 === i.low ? Number.MAX_VALUE : +i.low }, r = void 0 === i.high, a = void 0 === i.low; return (r || a) && function e(t) { if (void 0 !== t) if (t instanceof Array) for (var i = 0; i < t.length; i++)e(t[i]); else { var o = n ? +t[n] : +t; r && o > s.high && (s.high = o), a && o < s.low && (s.low = o) } }(e), (i.referenceValue || 0 === i.referenceValue) && (s.high = Math.max(i.referenceValue, s.high), s.low = Math.min(i.referenceValue, s.low)), s.high <= s.low && (0 === s.low ? s.high = 1 : s.low < 0 ? s.high = 0 : (s.high > 0 || (s.high = 1), s.low = 0)), s }, t.isNumeric = function (e) { return null !== e && isFinite(e) }, t.isFalseyButZero = function (e) { return !e && 0 !== e }, t.getNumberOrUndefined = function (e) { return t.isNumeric(e) ? +e : void 0 }, t.isMultiValue = function (e) { return "object" == typeof e && ("x" in e || "y" in e) }, t.getMultiValue = function (e, i) { return t.isMultiValue(e) ? t.getNumberOrUndefined(e[i || "y"]) : t.getNumberOrUndefined(e) }, t.rho = function (e) { if (1 === e) return e; function t(e, i) { return e % i == 0 ? i : t(i, e % i) } function i(e) { return e * e + 1 } var n, s = 2, r = 2; if (e % 2 == 0) return 2; do { s = i(s) % e, r = i(i(r)) % e, n = t(Math.abs(s - r), e) } while (1 === n); return n }, t.getBounds = function (e, i, n, s) { var r, a, o, l = 0, h = { high: i.high, low: i.low }; h.valueRange = h.high - h.low, h.oom = t.orderOfMagnitude(h.valueRange), h.step = Math.pow(10, h.oom), h.min = Math.floor(h.low / h.step) * h.step, h.max = Math.ceil(h.high / h.step) * h.step, h.range = h.max - h.min, h.numberOfSteps = Math.round(h.range / h.step); var u = t.projectLength(e, h.step, h) < n, c = s ? t.rho(h.range) : 0; if (s && t.projectLength(e, 1, h) >= n) h.step = 1; else if (s && c < h.step && t.projectLength(e, c, h) >= n) h.step = c; else for (; ;) { if (u && t.projectLength(e, h.step, h) <= n) h.step *= 2; else { if (u || !(t.projectLength(e, h.step / 2, h) >= n)) break; if (h.step /= 2, s && h.step % 1 != 0) { h.step *= 2; break } } if (l++ > 1e3) throw new Error("Exceeded maximum number of iterations while optimizing scale step!") } var d = 2221e-19; function p(e, t) { return e === (e += t) && (e *= 1 + (t > 0 ? d : -d)), e } for (h.step = Math.max(h.step, d), a = h.min, o = h.max; a + h.step <= h.low;)a = p(a, h.step); for (; o - h.step >= h.high;)o = p(o, -h.step); h.min = a, h.max = o, h.range = h.max - h.min; var f = []; for (r = h.min; r <= h.max; r = p(r, h.step)) { var m = t.roundWithPrecision(r); m !== f[f.length - 1] && f.push(m) } return h.values = f, h }, t.polarToCartesian = function (e, t, i, n) { var s = (n - 90) * Math.PI / 180; return { x: e + i * Math.cos(s), y: t + i * Math.sin(s) } }, t.createChartRect = function (e, i, n) { var s = !(!i.axisX && !i.axisY), r = s ? i.axisY.offset : 0, a = s ? i.axisX.offset : 0, o = e.width() || t.quantity(i.width).value || 0, l = e.height() || t.quantity(i.height).value || 0, h = t.normalizePadding(i.chartPadding, n); o = Math.max(o, r + h.left + h.right), l = Math.max(l, a + h.top + h.bottom); var u = { padding: h, width: function () { return this.x2 - this.x1 }, height: function () { return this.y1 - this.y2 } }; return s ? ("start" === i.axisX.position ? (u.y2 = h.top + a, u.y1 = Math.max(l - h.bottom, u.y2 + 1)) : (u.y2 = h.top, u.y1 = Math.max(l - h.bottom - a, u.y2 + 1)), "start" === i.axisY.position ? (u.x1 = h.left + r, u.x2 = Math.max(o - h.right, u.x1 + 1)) : (u.x1 = h.left, u.x2 = Math.max(o - h.right - r, u.x1 + 1))) : (u.x1 = h.left, u.x2 = Math.max(o - h.right, u.x1 + 1), u.y2 = h.top, u.y1 = Math.max(l - h.bottom, u.y2 + 1)), u }, t.createGrid = function (e, i, n, s, r, a, o, l) { var h = {}; h[n.units.pos + "1"] = e, h[n.units.pos + "2"] = e, h[n.counterUnits.pos + "1"] = s, h[n.counterUnits.pos + "2"] = s + r; var u = a.elem("line", h, o.join(" ")); l.emit("draw", t.extend({ type: "grid", axis: n, index: i, group: a, element: u }, h)) }, t.createGridBackground = function (e, t, i, n) { var s = e.elem("rect", { x: t.x1, y: t.y2, width: t.width(), height: t.height() }, i, !0); n.emit("draw", { type: "gridBackground", group: e, element: s }) }, t.createLabel = function (e, i, s, r, a, o, l, h, u, c, d) { var p, f = {}; if (f[a.units.pos] = e + l[a.units.pos], f[a.counterUnits.pos] = l[a.counterUnits.pos], f[a.units.len] = i, f[a.counterUnits.len] = Math.max(0, o - 10), c) { var m = n.createElement("span"); m.className = u.join(" "), m.setAttribute("xmlns", t.namespaces.xhtml), m.innerText = r[s], m.style[a.units.len] = Math.round(f[a.units.len]) + "px", m.style[a.counterUnits.len] = Math.round(f[a.counterUnits.len]) + "px", p = h.foreignObject(m, t.extend({ style: "overflow: visible;" }, f)) } else p = h.elem("text", f, u.join(" ")).text(r[s]); d.emit("draw", t.extend({ type: "label", axis: a, index: s, group: h, element: p, text: r[s] }, f)) }, t.getSeriesOption = function (e, t, i) { if (e.name && t.series && t.series[e.name]) { var n = t.series[e.name]; return n.hasOwnProperty(i) ? n[i] : t[i] } return t[i] }, t.optionsProvider = function (e, n, s) { var r, a, o = t.extend({}, e), l = []; function h(e) { var l = r; if (r = t.extend({}, o), n) for (a = 0; a < n.length; a++) { i.matchMedia(n[a][0]).matches && (r = t.extend(r, n[a][1])) } s && e && s.emit("optionsChanged", { previousOptions: l, currentOptions: r }) } if (!i.matchMedia) throw "window.matchMedia not found! Make sure you're using a polyfill."; if (n) for (a = 0; a < n.length; a++) { var u = i.matchMedia(n[a][0]); u.addListener(h), l.push(u) } return h(), { removeMediaQueryListeners: function () { l.forEach((function (e) { e.removeListener(h) })) }, getCurrentOptions: function () { return t.extend({}, r) } } }, t.splitIntoSegments = function (e, i, n) { n = t.extend({}, { increasingX: !1, fillHoles: !1 }, n); for (var s = [], r = !0, a = 0; a < e.length; a += 2)void 0 === t.getMultiValue(i[a / 2].value) ? n.fillHoles || (r = !0) : (n.increasingX && a >= 2 && e[a] <= e[a - 2] && (r = !0), r && (s.push({ pathCoordinates: [], valueData: [] }), r = !1), s[s.length - 1].pathCoordinates.push(e[a], e[a + 1]), s[s.length - 1].valueData.push(i[a / 2])); return s } }(this || global, e), function (e, t) { "use strict"; t.Interpolation = {}, t.Interpolation.none = function (e) { return e = t.extend({}, { fillHoles: !1 }, e), function (i, n) { for (var s = new t.Svg.Path, r = !0, a = 0; a < i.length; a += 2) { var o = i[a], l = i[a + 1], h = n[a / 2]; void 0 !== t.getMultiValue(h.value) ? (r ? s.move(o, l, !1, h) : s.line(o, l, !1, h), r = !1) : e.fillHoles || (r = !0) } return s } }, t.Interpolation.simple = function (e) { e = t.extend({}, { divisor: 2, fillHoles: !1 }, e); var i = 1 / Math.max(1, e.divisor); return function (n, s) { for (var r, a, o, l = new t.Svg.Path, h = 0; h < n.length; h += 2) { var u = n[h], c = n[h + 1], d = (u - r) * i, p = s[h / 2]; void 0 !== p.value ? (void 0 === o ? l.move(u, c, !1, p) : l.curve(r + d, a, u - d, c, u, c, !1, p), r = u, a = c, o = p) : e.fillHoles || (r = u = o = void 0) } return l } }, t.Interpolation.cardinal = function (e) { e = t.extend({}, { tension: 1, fillHoles: !1 }, e); var i = Math.min(1, Math.max(0, e.tension)), n = 1 - i; return function s(r, a) { var o = t.splitIntoSegments(r, a, { fillHoles: e.fillHoles }); if (o.length) { if (o.length > 1) { var l = []; return o.forEach((function (e) { l.push(s(e.pathCoordinates, e.valueData)) })), t.Svg.Path.join(l) } if (r = o[0].pathCoordinates, a = o[0].valueData, r.length <= 4) return t.Interpolation.none()(r, a); for (var h = (new t.Svg.Path).move(r[0], r[1], !1, a[0]), u = 0, c = r.length; c - 2 > u; u += 2) { var d = [{ x: +r[u - 2], y: +r[u - 1] }, { x: +r[u], y: +r[u + 1] }, { x: +r[u + 2], y: +r[u + 3] }, { x: +r[u + 4], y: +r[u + 5] }]; c - 4 === u ? d[3] = d[2] : u || (d[0] = { x: +r[u], y: +r[u + 1] }), h.curve(i * (-d[0].x + 6 * d[1].x + d[2].x) / 6 + n * d[2].x, i * (-d[0].y + 6 * d[1].y + d[2].y) / 6 + n * d[2].y, i * (d[1].x + 6 * d[2].x - d[3].x) / 6 + n * d[2].x, i * (d[1].y + 6 * d[2].y - d[3].y) / 6 + n * d[2].y, d[2].x, d[2].y, !1, a[(u + 2) / 2]) } return h } return t.Interpolation.none()([]) } }, t.Interpolation.monotoneCubic = function (e) { return e = t.extend({}, { fillHoles: !1 }, e), function i(n, s) { var r = t.splitIntoSegments(n, s, { fillHoles: e.fillHoles, increasingX: !0 }); if (r.length) { if (r.length > 1) { var a = []; return r.forEach((function (e) { a.push(i(e.pathCoordinates, e.valueData)) })), t.Svg.Path.join(a) } if (n = r[0].pathCoordinates, s = r[0].valueData, n.length <= 4) return t.Interpolation.none()(n, s); var o, l, h = [], u = [], c = n.length / 2, d = [], p = [], f = [], m = []; for (o = 0; o < c; o++)h[o] = n[2 * o], u[o] = n[2 * o + 1]; for (o = 0; o < c - 1; o++)f[o] = u[o + 1] - u[o], m[o] = h[o + 1] - h[o], p[o] = f[o] / m[o]; for (d[0] = p[0], d[c - 1] = p[c - 2], o = 1; o < c - 1; o++)0 === p[o] || 0 === p[o - 1] || p[o - 1] > 0 != p[o] > 0 ? d[o] = 0 : (d[o] = 3 * (m[o - 1] + m[o]) / ((2 * m[o] + m[o - 1]) / p[o - 1] + (m[o] + 2 * m[o - 1]) / p[o]), isFinite(d[o]) || (d[o] = 0)); for (l = (new t.Svg.Path).move(h[0], u[0], !1, s[0]), o = 0; o < c - 1; o++)l.curve(h[o] + m[o] / 3, u[o] + d[o] * m[o] / 3, h[o + 1] - m[o] / 3, u[o + 1] - d[o + 1] * m[o] / 3, h[o + 1], u[o + 1], !1, s[o + 1]); return l } return t.Interpolation.none()([]) } }, t.Interpolation.step = function (e) { return e = t.extend({}, { postpone: !0, fillHoles: !1 }, e), function (i, n) { for (var s, r, a, o = new t.Svg.Path, l = 0; l < i.length; l += 2) { var h = i[l], u = i[l + 1], c = n[l / 2]; void 0 !== c.value ? (void 0 === a ? o.move(h, u, !1, c) : (e.postpone ? o.line(h, r, !1, a) : o.line(s, u, !1, c), o.line(h, u, !1, c)), s = h, r = u, a = c) : e.fillHoles || (s = r = a = void 0) } return o } } }(this || global, e), function (e, t) { "use strict"; t.EventEmitter = function () { var e = []; return { addEventHandler: function (t, i) { e[t] = e[t] || [], e[t].push(i) }, removeEventHandler: function (t, i) { e[t] && (i ? (e[t].splice(e[t].indexOf(i), 1), 0 === e[t].length && delete e[t]) : delete e[t]) }, emit: function (t, i) { e[t] && e[t].forEach((function (e) { e(i) })), e["*"] && e["*"].forEach((function (e) { e(t, i) })) } } } }(this || global, e), function (e, t) { "use strict"; t.Class = { extend: function (e, i) { var n = i || this.prototype || t.Class, s = Object.create(n); t.Class.cloneDefinitions(s, e); var r = function () { var e, i = s.constructor || function () { }; return e = this === t ? Object.create(s) : this, i.apply(e, Array.prototype.slice.call(arguments, 0)), e }; return r.prototype = s, r.super = n, r.extend = this.extend, r }, cloneDefinitions: function () { var e = function (e) { var t = []; if (e.length) for (var i = 0; i < e.length; i++)t.push(e[i]); return t }(arguments), t = e[0]; return e.splice(1, e.length - 1).forEach((function (e) { Object.getOwnPropertyNames(e).forEach((function (i) { delete t[i], Object.defineProperty(t, i, Object.getOwnPropertyDescriptor(e, i)) })) })), t } } }(this || global, e), function (e, t) { "use strict"; var i = e.window; function n() { i.addEventListener("resize", this.resizeListener), this.optionsProvider = t.optionsProvider(this.options, this.responsiveOptions, this.eventEmitter), this.eventEmitter.addEventHandler("optionsChanged", function () { this.update() }.bind(this)), this.options.plugins && this.options.plugins.forEach(function (e) { e instanceof Array ? e[0](this, e[1]) : e(this) }.bind(this)), this.eventEmitter.emit("data", { type: "initial", data: this.data }), this.createChart(this.optionsProvider.getCurrentOptions()), this.initializeTimeoutId = void 0 } t.Base = t.Class.extend({ constructor: function (e, i, s, r, a) { this.container = t.querySelector(e), this.data = i || {}, this.data.labels = this.data.labels || [], this.data.series = this.data.series || [], this.defaultOptions = s, this.options = r, this.responsiveOptions = a, this.eventEmitter = t.EventEmitter(), this.supportsForeignObject = t.Svg.isSupported("Extensibility"), this.supportsAnimations = t.Svg.isSupported("AnimationEventsAttribute"), this.resizeListener = function () { this.update() }.bind(this), this.container && (this.container.__chartist__ && this.container.__chartist__.detach(), this.container.__chartist__ = this), this.initializeTimeoutId = setTimeout(n.bind(this), 0) }, optionsProvider: void 0, container: void 0, svg: void 0, eventEmitter: void 0, createChart: function () { throw new Error("Base chart type can't be instantiated!") }, update: function (e, i, n) { return e && (this.data = e || {}, this.data.labels = this.data.labels || [], this.data.series = this.data.series || [], this.eventEmitter.emit("data", { type: "update", data: this.data })), i && (this.options = t.extend({}, n ? this.options : this.defaultOptions, i), this.initializeTimeoutId || (this.optionsProvider.removeMediaQueryListeners(), this.optionsProvider = t.optionsProvider(this.options, this.responsiveOptions, this.eventEmitter))), this.initializeTimeoutId || this.createChart(this.optionsProvider.getCurrentOptions()), this }, detach: function () { return this.initializeTimeoutId ? i.clearTimeout(this.initializeTimeoutId) : (i.removeEventListener("resize", this.resizeListener), this.optionsProvider.removeMediaQueryListeners()), this }, on: function (e, t) { return this.eventEmitter.addEventHandler(e, t), this }, off: function (e, t) { return this.eventEmitter.removeEventHandler(e, t), this }, version: t.version, supportsForeignObject: !1 }) }(this || global, e), function (e, t) { "use strict"; var i = e.document; t.Svg = t.Class.extend({ constructor: function (e, n, s, r, a) { e instanceof Element ? this._node = e : (this._node = i.createElementNS(t.namespaces.svg, e), "svg" === e && this.attr({ "xmlns:ct": t.namespaces.ct })), n && this.attr(n), s && this.addClass(s), r && (a && r._node.firstChild ? r._node.insertBefore(this._node, r._node.firstChild) : r._node.appendChild(this._node)) }, attr: function (e, i) { return "string" == typeof e ? i ? this._node.getAttributeNS(i, e) : this._node.getAttribute(e) : (Object.keys(e).forEach(function (i) { if (void 0 !== e[i]) if (-1 !== i.indexOf(":")) { var n = i.split(":"); this._node.setAttributeNS(t.namespaces[n[0]], i, e[i]) } else this._node.setAttribute(i, e[i]) }.bind(this)), this) }, elem: function (e, i, n, s) { return new t.Svg(e, i, n, this, s) }, parent: function () { return this._node.parentNode instanceof SVGElement ? new t.Svg(this._node.parentNode) : null }, root: function () { for (var e = this._node; "svg" !== e.nodeName;)e = e.parentNode; return new t.Svg(e) }, querySelector: function (e) { var i = this._node.querySelector(e); return i ? new t.Svg(i) : null }, querySelectorAll: function (e) { var i = this._node.querySelectorAll(e); return i.length ? new t.Svg.List(i) : null }, getNode: function () { return this._node }, foreignObject: function (e, n, s, r) { if ("string" == typeof e) { var a = i.createElement("div"); a.innerHTML = e, e = a.firstChild } e.setAttribute("xmlns", t.namespaces.xmlns); var o = this.elem("foreignObject", n, s, r); return o._node.appendChild(e), o }, text: function (e) { return this._node.appendChild(i.createTextNode(e)), this }, empty: function () { for (; this._node.firstChild;)this._node.removeChild(this._node.firstChild); return this }, remove: function () { return this._node.parentNode.removeChild(this._node), this.parent() }, replace: function (e) { return this._node.parentNode.replaceChild(e._node, this._node), e }, append: function (e, t) { return t && this._node.firstChild ? this._node.insertBefore(e._node, this._node.firstChild) : this._node.appendChild(e._node), this }, classes: function () { return this._node.getAttribute("class") ? this._node.getAttribute("class").trim().split(/\s+/) : [] }, addClass: function (e) { return this._node.setAttribute("class", this.classes(this._node).concat(e.trim().split(/\s+/)).filter((function (e, t, i) { return i.indexOf(e) === t })).join(" ")), this }, removeClass: function (e) { var t = e.trim().split(/\s+/); return this._node.setAttribute("class", this.classes(this._node).filter((function (e) { return -1 === t.indexOf(e) })).join(" ")), this }, removeAllClasses: function () { return this._node.setAttribute("class", ""), this }, height: function () { return this._node.getBoundingClientRect().height }, width: function () { return this._node.getBoundingClientRect().width }, animate: function (e, i, n) { return void 0 === i && (i = !0), Object.keys(e).forEach(function (s) { function r(e, i) { var r, a, o, l = {}; e.easing && (o = e.easing instanceof Array ? e.easing : t.Svg.Easing[e.easing], delete e.easing), e.begin = t.ensureUnit(e.begin, "ms"), e.dur = t.ensureUnit(e.dur, "ms"), o && (e.calcMode = "spline", e.keySplines = o.join(" "), e.keyTimes = "0;1"), i && (e.fill = "freeze", l[s] = e.from, this.attr(l), a = t.quantity(e.begin || 0).value, e.begin = "indefinite"), r = this.elem("animate", t.extend({ attributeName: s }, e)), i && setTimeout(function () { try { r._node.beginElement() } catch (t) { l[s] = e.to, this.attr(l), r.remove() } }.bind(this), a), n && r._node.addEventListener("beginEvent", function () { n.emit("animationBegin", { element: this, animate: r._node, params: e }) }.bind(this)), r._node.addEventListener("endEvent", function () { n && n.emit("animationEnd", { element: this, animate: r._node, params: e }), i && (l[s] = e.to, this.attr(l), r.remove()) }.bind(this)) } e[s] instanceof Array ? e[s].forEach(function (e) { r.bind(this)(e, !1) }.bind(this)) : r.bind(this)(e[s], i) }.bind(this)), this } }), t.Svg.isSupported = function (e) { return i.implementation.hasFeature("http://www.w3.org/TR/SVG11/feature#" + e, "1.1") }; t.Svg.Easing = { easeInSine: [.47, 0, .745, .715], easeOutSine: [.39, .575, .565, 1], easeInOutSine: [.445, .05, .55, .95], easeInQuad: [.55, .085, .68, .53], easeOutQuad: [.25, .46, .45, .94], easeInOutQuad: [.455, .03, .515, .955], easeInCubic: [.55, .055, .675, .19], easeOutCubic: [.215, .61, .355, 1], easeInOutCubic: [.645, .045, .355, 1], easeInQuart: [.895, .03, .685, .22], easeOutQuart: [.165, .84, .44, 1], easeInOutQuart: [.77, 0, .175, 1], easeInQuint: [.755, .05, .855, .06], easeOutQuint: [.23, 1, .32, 1], easeInOutQuint: [.86, 0, .07, 1], easeInExpo: [.95, .05, .795, .035], easeOutExpo: [.19, 1, .22, 1], easeInOutExpo: [1, 0, 0, 1], easeInCirc: [.6, .04, .98, .335], easeOutCirc: [.075, .82, .165, 1], easeInOutCirc: [.785, .135, .15, .86], easeInBack: [.6, -.28, .735, .045], easeOutBack: [.175, .885, .32, 1.275], easeInOutBack: [.68, -.55, .265, 1.55] }, t.Svg.List = t.Class.extend({ constructor: function (e) { var i = this; this.svgElements = []; for (var n = 0; n < e.length; n++)this.svgElements.push(new t.Svg(e[n])); Object.keys(t.Svg.prototype).filter((function (e) { return -1 === ["constructor", "parent", "querySelector", "querySelectorAll", "replace", "append", "classes", "height", "width"].indexOf(e) })).forEach((function (e) { i[e] = function () { var n = Array.prototype.slice.call(arguments, 0); return i.svgElements.forEach((function (i) { t.Svg.prototype[e].apply(i, n) })), i } })) } }) }(this || global, e), function (e, t) { "use strict"; var i = { m: ["x", "y"], l: ["x", "y"], c: ["x1", "y1", "x2", "y2", "x", "y"], a: ["rx", "ry", "xAr", "lAf", "sf", "x", "y"] }, n = { accuracy: 3 }; function s(e, i, n, s, r, a) { var o = t.extend({ command: r ? e.toLowerCase() : e.toUpperCase() }, i, a ? { data: a } : {}); n.splice(s, 0, o) } function r(e, t) { e.forEach((function (n, s) { i[n.command.toLowerCase()].forEach((function (i, r) { t(n, i, s, r, e) })) })) } t.Svg.Path = t.Class.extend({ constructor: function (e, i) { this.pathElements = [], this.pos = 0, this.close = e, this.options = t.extend({}, n, i) }, position: function (e) { return void 0 !== e ? (this.pos = Math.max(0, Math.min(this.pathElements.length, e)), this) : this.pos }, remove: function (e) { return this.pathElements.splice(this.pos, e), this }, move: function (e, t, i, n) { return s("M", { x: +e, y: +t }, this.pathElements, this.pos++, i, n), this }, line: function (e, t, i, n) { return s("L", { x: +e, y: +t }, this.pathElements, this.pos++, i, n), this }, curve: function (e, t, i, n, r, a, o, l) { return s("C", { x1: +e, y1: +t, x2: +i, y2: +n, x: +r, y: +a }, this.pathElements, this.pos++, o, l), this }, arc: function (e, t, i, n, r, a, o, l, h) { return s("A", { rx: +e, ry: +t, xAr: +i, lAf: +n, sf: +r, x: +a, y: +o }, this.pathElements, this.pos++, l, h), this }, scale: function (e, t) { return r(this.pathElements, (function (i, n) { i[n] *= "x" === n[0] ? e : t })), this }, translate: function (e, t) { return r(this.pathElements, (function (i, n) { i[n] += "x" === n[0] ? e : t })), this }, transform: function (e) { return r(this.pathElements, (function (t, i, n, s, r) { var a = e(t, i, n, s, r); (a || 0 === a) && (t[i] = a) })), this }, parse: function (e) { var n = e.replace(/([A-Za-z])([0-9])/g, "$1 $2").replace(/([0-9])([A-Za-z])/g, "$1 $2").split(/[\s,]+/).reduce((function (e, t) { return t.match(/[A-Za-z]/) && e.push([]), e[e.length - 1].push(t), e }), []); "Z" === n[n.length - 1][0].toUpperCase() && n.pop(); var s = n.map((function (e) { var n = e.shift(), s = i[n.toLowerCase()]; return t.extend({ command: n }, s.reduce((function (t, i, n) { return t[i] = +e[n], t }), {})) })), r = [this.pos, 0]; return Array.prototype.push.apply(r, s), Array.prototype.splice.apply(this.pathElements, r), this.pos += s.length, this }, stringify: function () { var e = Math.pow(10, this.options.accuracy); return this.pathElements.reduce(function (t, n) { var s = i[n.command.toLowerCase()].map(function (t) { return this.options.accuracy ? Math.round(n[t] * e) / e : n[t] }.bind(this)); return t + n.command + s.join(",") }.bind(this), "") + (this.close ? "Z" : "") }, clone: function (e) { var i = new t.Svg.Path(e || this.close); return i.pos = this.pos, i.pathElements = this.pathElements.slice().map((function (e) { return t.extend({}, e) })), i.options = t.extend({}, this.options), i }, splitByCommand: function (e) { var i = [new t.Svg.Path]; return this.pathElements.forEach((function (n) { n.command === e.toUpperCase() && 0 !== i[i.length - 1].pathElements.length && i.push(new t.Svg.Path), i[i.length - 1].pathElements.push(n) })), i } }), t.Svg.Path.elementDescriptions = i, t.Svg.Path.join = function (e, i, n) { for (var s = new t.Svg.Path(i, n), r = 0; r < e.length; r++)for (var a = e[r], o = 0; o < a.pathElements.length; o++)s.pathElements.push(a.pathElements[o]); return s } }(this || global, e), function (e, t) { "use strict"; e.window, e.document; var i = { x: { pos: "x", len: "width", dir: "horizontal", rectStart: "x1", rectEnd: "x2", rectOffset: "y2" }, y: { pos: "y", len: "height", dir: "vertical", rectStart: "y2", rectEnd: "y1", rectOffset: "x1" } }; t.Axis = t.Class.extend({ constructor: function (e, t, n, s) { this.units = e, this.counterUnits = e === i.x ? i.y : i.x, this.chartRect = t, this.axisLength = t[e.rectEnd] - t[e.rectStart], this.gridOffset = t[e.rectOffset], this.ticks = n, this.options = s }, createGridAndLabels: function (e, i, n, s, r) { var a = s["axis" + this.units.pos.toUpperCase()], o = this.ticks.map(this.projectValue.bind(this)), l = this.ticks.map(a.labelInterpolationFnc); o.forEach(function (h, u) { var c, d = { x: 0, y: 0 }; c = o[u + 1] ? o[u + 1] - h : Math.max(this.axisLength - h, 30), t.isFalseyButZero(l[u]) && "" !== l[u] || ("x" === this.units.pos ? (h = this.chartRect.x1 + h, d.x = s.axisX.labelOffset.x, "start" === s.axisX.position ? d.y = this.chartRect.padding.top + s.axisX.labelOffset.y + (n ? 5 : 20) : d.y = this.chartRect.y1 + s.axisX.labelOffset.y + (n ? 5 : 20)) : (h = this.chartRect.y1 - h, d.y = s.axisY.labelOffset.y - (n ? c : 0), "start" === s.axisY.position ? d.x = n ? this.chartRect.padding.left + s.axisY.labelOffset.x : this.chartRect.x1 - 10 : d.x = this.chartRect.x2 + s.axisY.labelOffset.x + 10), a.showGrid && t.createGrid(h, u, this, this.gridOffset, this.chartRect[this.counterUnits.len](), e, [s.classNames.grid, s.classNames[this.units.dir]], r), a.showLabel && t.createLabel(h, c, u, l, this, a.offset, d, i, [s.classNames.label, s.classNames[this.units.dir], "start" === a.position ? s.classNames[a.position] : s.classNames.end], n, r)) }.bind(this)) }, projectValue: function (e, t, i) { throw new Error("Base axis can't be instantiated!") } }), t.Axis.units = i }(this || global, e), function (e, t) { "use strict"; e.window, e.document; t.AutoScaleAxis = t.Axis.extend({ constructor: function (e, i, n, s) { var r = s.highLow || t.getHighLow(i, s, e.pos); this.bounds = t.getBounds(n[e.rectEnd] - n[e.rectStart], r, s.scaleMinSpace || 20, s.onlyInteger), this.range = { min: this.bounds.min, max: this.bounds.max }, t.AutoScaleAxis.super.constructor.call(this, e, n, this.bounds.values, s) }, projectValue: function (e) { return this.axisLength * (+t.getMultiValue(e, this.units.pos) - this.bounds.min) / this.bounds.range } }) }(this || global, e), function (e, t) { "use strict"; e.window, e.document; t.FixedScaleAxis = t.Axis.extend({ constructor: function (e, i, n, s) { var r = s.highLow || t.getHighLow(i, s, e.pos); this.divisor = s.divisor || 1, this.ticks = s.ticks || t.times(this.divisor).map(function (e, t) { return r.low + (r.high - r.low) / this.divisor * t }.bind(this)), this.ticks.sort((function (e, t) { return e - t })), this.range = { min: r.low, max: r.high }, t.FixedScaleAxis.super.constructor.call(this, e, n, this.ticks, s), this.stepLength = this.axisLength / this.divisor }, projectValue: function (e) { return this.axisLength * (+t.getMultiValue(e, this.units.pos) - this.range.min) / (this.range.max - this.range.min) } }) }(this || global, e), function (e, t) { "use strict"; e.window, e.document; t.StepAxis = t.Axis.extend({ constructor: function (e, i, n, s) { t.StepAxis.super.constructor.call(this, e, n, s.ticks, s); var r = Math.max(1, s.ticks.length - (s.stretch ? 1 : 0)); this.stepLength = this.axisLength / r }, projectValue: function (e, t) { return this.stepLength * t } }) }(this || global, e), function (e, t) { "use strict"; e.window, e.document; var i = { axisX: { offset: 30, position: "end", labelOffset: { x: 0, y: 0 }, showLabel: !0, showGrid: !0, labelInterpolationFnc: t.noop, type: void 0 }, axisY: { offset: 40, position: "start", labelOffset: { x: 0, y: 0 }, showLabel: !0, showGrid: !0, labelInterpolationFnc: t.noop, type: void 0, scaleMinSpace: 20, onlyInteger: !1 }, width: void 0, height: void 0, showLine: !0, showPoint: !0, showArea: !1, areaBase: 0, lineSmooth: !0, showGridBackground: !1, low: void 0, high: void 0, chartPadding: { top: 15, right: 15, bottom: 5, left: 10 }, fullWidth: !1, reverseData: !1, classNames: { chart: "ct-chart-line", label: "ct-label", labelGroup: "ct-labels", series: "ct-series", line: "ct-line", point: "ct-point", area: "ct-area", grid: "ct-grid", gridGroup: "ct-grids", gridBackground: "ct-grid-background", vertical: "ct-vertical", horizontal: "ct-horizontal", start: "ct-start", end: "ct-end" } }; t.Line = t.Base.extend({ constructor: function (e, n, s, r) { t.Line.super.constructor.call(this, e, n, i, t.extend({}, i, s), r) }, createChart: function (e) { var n = t.normalizeData(this.data, e.reverseData, !0); this.svg = t.createSvg(this.container, e.width, e.height, e.classNames.chart); var s, r, a = this.svg.elem("g").addClass(e.classNames.gridGroup), o = this.svg.elem("g"), l = this.svg.elem("g").addClass(e.classNames.labelGroup), h = t.createChartRect(this.svg, e, i.padding); s = void 0 === e.axisX.type ? new t.StepAxis(t.Axis.units.x, n.normalized.series, h, t.extend({}, e.axisX, { ticks: n.normalized.labels, stretch: e.fullWidth })) : e.axisX.type.call(t, t.Axis.units.x, n.normalized.series, h, e.axisX), r = void 0 === e.axisY.type ? new t.AutoScaleAxis(t.Axis.units.y, n.normalized.series, h, t.extend({}, e.axisY, { high: t.isNumeric(e.high) ? e.high : e.axisY.high, low: t.isNumeric(e.low) ? e.low : e.axisY.low })) : e.axisY.type.call(t, t.Axis.units.y, n.normalized.series, h, e.axisY), s.createGridAndLabels(a, l, this.supportsForeignObject, e, this.eventEmitter), r.createGridAndLabels(a, l, this.supportsForeignObject, e, this.eventEmitter), e.showGridBackground && t.createGridBackground(a, h, e.classNames.gridBackground, this.eventEmitter), n.raw.series.forEach(function (i, a) { var l = o.elem("g"); l.attr({ "ct:series-name": i.name, "ct:meta": t.serialize(i.meta) }), l.addClass([e.classNames.series, i.className || e.classNames.series + "-" + t.alphaNumerate(a)].join(" ")); var u = [], c = []; n.normalized.series[a].forEach(function (e, o) { var l = { x: h.x1 + s.projectValue(e, o, n.normalized.series[a]), y: h.y1 - r.projectValue(e, o, n.normalized.series[a]) }; u.push(l.x, l.y), c.push({ value: e, valueIndex: o, meta: t.getMetaData(i, o) }) }.bind(this)); var d = { lineSmooth: t.getSeriesOption(i, e, "lineSmooth"), showPoint: t.getSeriesOption(i, e, "showPoint"), showLine: t.getSeriesOption(i, e, "showLine"), showArea: t.getSeriesOption(i, e, "showArea"), areaBase: t.getSeriesOption(i, e, "areaBase") }, p = ("function" == typeof d.lineSmooth ? d.lineSmooth : d.lineSmooth ? t.Interpolation.monotoneCubic() : t.Interpolation.none())(u, c); if (d.showPoint && p.pathElements.forEach(function (n) { var o = l.elem("line", { x1: n.x, y1: n.y, x2: n.x + .01, y2: n.y }, e.classNames.point).attr({ "ct:value": [n.data.value.x, n.data.value.y].filter(t.isNumeric).join(","), "ct:meta": t.serialize(n.data.meta) }); this.eventEmitter.emit("draw", { type: "point", value: n.data.value, index: n.data.valueIndex, meta: n.data.meta, series: i, seriesIndex: a, axisX: s, axisY: r, group: l, element: o, x: n.x, y: n.y }) }.bind(this)), d.showLine) { var f = l.elem("path", { d: p.stringify() }, e.classNames.line, !0); this.eventEmitter.emit("draw", { type: "line", values: n.normalized.series[a], path: p.clone(), chartRect: h, index: a, series: i, seriesIndex: a, seriesMeta: i.meta, axisX: s, axisY: r, group: l, element: f }) } if (d.showArea && r.range) { var m = Math.max(Math.min(d.areaBase, r.range.max), r.range.min), g = h.y1 - r.projectValue(m); p.splitByCommand("M").filter((function (e) { return e.pathElements.length > 1 })).map((function (e) { var t = e.pathElements[0], i = e.pathElements[e.pathElements.length - 1]; return e.clone(!0).position(0).remove(1).move(t.x, g).line(t.x, t.y).position(e.pathElements.length + 1).line(i.x, g) })).forEach(function (t) { var o = l.elem("path", { d: t.stringify() }, e.classNames.area, !0); this.eventEmitter.emit("draw", { type: "area", values: n.normalized.series[a], path: t.clone(), series: i, seriesIndex: a, axisX: s, axisY: r, chartRect: h, index: a, group: l, element: o }) }.bind(this)) } }.bind(this)), this.eventEmitter.emit("created", { bounds: r.bounds, chartRect: h, axisX: s, axisY: r, svg: this.svg, options: e }) } }) }(this || global, e), function (e, t) { "use strict"; e.window, e.document; var i = { axisX: { offset: 30, position: "end", labelOffset: { x: 0, y: 0 }, showLabel: !0, showGrid: !0, labelInterpolationFnc: t.noop, scaleMinSpace: 30, onlyInteger: !1 }, axisY: { offset: 40, position: "start", labelOffset: { x: 0, y: 0 }, showLabel: !0, showGrid: !0, labelInterpolationFnc: t.noop, scaleMinSpace: 20, onlyInteger: !1 }, width: void 0, height: void 0, high: void 0, low: void 0, referenceValue: 0, chartPadding: { top: 15, right: 15, bottom: 5, left: 10 }, seriesBarDistance: 15, stackBars: !1, stackMode: "accumulate", horizontalBars: !1, distributeSeries: !1, reverseData: !1, showGridBackground: !1, classNames: { chart: "ct-chart-bar", horizontalBars: "ct-horizontal-bars", label: "ct-label", labelGroup: "ct-labels", series: "ct-series", bar: "ct-bar", grid: "ct-grid", gridGroup: "ct-grids", gridBackground: "ct-grid-background", vertical: "ct-vertical", horizontal: "ct-horizontal", start: "ct-start", end: "ct-end" } }; t.Bar = t.Base.extend({ constructor: function (e, n, s, r) { t.Bar.super.constructor.call(this, e, n, i, t.extend({}, i, s), r) }, createChart: function (e) { var n, s; e.distributeSeries ? (n = t.normalizeData(this.data, e.reverseData, e.horizontalBars ? "x" : "y")).normalized.series = n.normalized.series.map((function (e) { return [e] })) : n = t.normalizeData(this.data, e.reverseData, e.horizontalBars ? "x" : "y"), this.svg = t.createSvg(this.container, e.width, e.height, e.classNames.chart + (e.horizontalBars ? " " + e.classNames.horizontalBars : "")); var r = this.svg.elem("g").addClass(e.classNames.gridGroup), a = this.svg.elem("g"), o = this.svg.elem("g").addClass(e.classNames.labelGroup); if (e.stackBars && 0 !== n.normalized.series.length) { var l = t.serialMap(n.normalized.series, (function () { return Array.prototype.slice.call(arguments).map((function (e) { return e })).reduce((function (e, t) { return { x: e.x + (t && t.x) || 0, y: e.y + (t && t.y) || 0 } }), { x: 0, y: 0 }) })); s = t.getHighLow([l], e, e.horizontalBars ? "x" : "y") } else s = t.getHighLow(n.normalized.series, e, e.horizontalBars ? "x" : "y"); s.high = +e.high || (0 === e.high ? 0 : s.high), s.low = +e.low || (0 === e.low ? 0 : s.low); var h, u, c, d, p, f = t.createChartRect(this.svg, e, i.padding); u = e.distributeSeries && e.stackBars ? n.normalized.labels.slice(0, 1) : n.normalized.labels, e.horizontalBars ? (h = d = void 0 === e.axisX.type ? new t.AutoScaleAxis(t.Axis.units.x, n.normalized.series, f, t.extend({}, e.axisX, { highLow: s, referenceValue: 0 })) : e.axisX.type.call(t, t.Axis.units.x, n.normalized.series, f, t.extend({}, e.axisX, { highLow: s, referenceValue: 0 })), c = p = void 0 === e.axisY.type ? new t.StepAxis(t.Axis.units.y, n.normalized.series, f, { ticks: u }) : e.axisY.type.call(t, t.Axis.units.y, n.normalized.series, f, e.axisY)) : (c = d = void 0 === e.axisX.type ? new t.StepAxis(t.Axis.units.x, n.normalized.series, f, { ticks: u }) : e.axisX.type.call(t, t.Axis.units.x, n.normalized.series, f, e.axisX), h = p = void 0 === e.axisY.type ? new t.AutoScaleAxis(t.Axis.units.y, n.normalized.series, f, t.extend({}, e.axisY, { highLow: s, referenceValue: 0 })) : e.axisY.type.call(t, t.Axis.units.y, n.normalized.series, f, t.extend({}, e.axisY, { highLow: s, referenceValue: 0 }))); var m = e.horizontalBars ? f.x1 + h.projectValue(0) : f.y1 - h.projectValue(0), g = []; c.createGridAndLabels(r, o, this.supportsForeignObject, e, this.eventEmitter), h.createGridAndLabels(r, o, this.supportsForeignObject, e, this.eventEmitter), e.showGridBackground && t.createGridBackground(r, f, e.classNames.gridBackground, this.eventEmitter), n.raw.series.forEach(function (i, s) { var r, o, l = s - (n.raw.series.length - 1) / 2; r = e.distributeSeries && !e.stackBars ? c.axisLength / n.normalized.series.length / 2 : e.distributeSeries && e.stackBars ? c.axisLength / 2 : c.axisLength / n.normalized.series[s].length / 2, (o = a.elem("g")).attr({ "ct:series-name": i.name, "ct:meta": t.serialize(i.meta) }), o.addClass([e.classNames.series, i.className || e.classNames.series + "-" + t.alphaNumerate(s)].join(" ")), n.normalized.series[s].forEach(function (a, u) { var v, x, y, b; if (b = e.distributeSeries && !e.stackBars ? s : e.distributeSeries && e.stackBars ? 0 : u, v = e.horizontalBars ? { x: f.x1 + h.projectValue(a && a.x ? a.x : 0, u, n.normalized.series[s]), y: f.y1 - c.projectValue(a && a.y ? a.y : 0, b, n.normalized.series[s]) } : { x: f.x1 + c.projectValue(a && a.x ? a.x : 0, b, n.normalized.series[s]), y: f.y1 - h.projectValue(a && a.y ? a.y : 0, u, n.normalized.series[s]) }, c instanceof t.StepAxis && (c.options.stretch || (v[c.units.pos] += r * (e.horizontalBars ? -1 : 1)), v[c.units.pos] += e.stackBars || e.distributeSeries ? 0 : l * e.seriesBarDistance * (e.horizontalBars ? -1 : 1)), y = g[u] || m, g[u] = y - (m - v[c.counterUnits.pos]), void 0 !== a) { var w = {}; w[c.units.pos + "1"] = v[c.units.pos], w[c.units.pos + "2"] = v[c.units.pos], !e.stackBars || "accumulate" !== e.stackMode && e.stackMode ? (w[c.counterUnits.pos + "1"] = m, w[c.counterUnits.pos + "2"] = v[c.counterUnits.pos]) : (w[c.counterUnits.pos + "1"] = y, w[c.counterUnits.pos + "2"] = g[u]), w.x1 = Math.min(Math.max(w.x1, f.x1), f.x2), w.x2 = Math.min(Math.max(w.x2, f.x1), f.x2), w.y1 = Math.min(Math.max(w.y1, f.y2), f.y1), w.y2 = Math.min(Math.max(w.y2, f.y2), f.y1); var E = t.getMetaData(i, u); x = o.elem("line", w, e.classNames.bar).attr({ "ct:value": [a.x, a.y].filter(t.isNumeric).join(","), "ct:meta": t.serialize(E) }), this.eventEmitter.emit("draw", t.extend({ type: "bar", value: a, index: u, meta: E, series: i, seriesIndex: s, axisX: d, axisY: p, chartRect: f, group: o, element: x }, w)) } }.bind(this)) }.bind(this)), this.eventEmitter.emit("created", { bounds: h.bounds, chartRect: f, axisX: d, axisY: p, svg: this.svg, options: e }) } }) }(this || global, e), function (e, t) { "use strict"; e.window, e.document; var i = { width: void 0, height: void 0, chartPadding: 5, classNames: { chartPie: "ct-chart-pie", chartDonut: "ct-chart-donut", series: "ct-series", slicePie: "ct-slice-pie", sliceDonut: "ct-slice-donut", sliceDonutSolid: "ct-slice-donut-solid", label: "ct-label" }, startAngle: 0, total: void 0, donut: !1, donutSolid: !1, donutWidth: 60, showLabel: !0, labelOffset: 0, labelPosition: "inside", labelInterpolationFnc: t.noop, labelDirection: "neutral", reverseData: !1, ignoreEmptyValues: !1 }; function n(e, t, i) { var n = t.x > e.x; return n && "explode" === i || !n && "implode" === i ? "start" : n && "implode" === i || !n && "explode" === i ? "end" : "middle" } t.Pie = t.Base.extend({ constructor: function (e, n, s, r) { t.Pie.super.constructor.call(this, e, n, i, t.extend({}, i, s), r) }, createChart: function (e) { var s, r, a, o, l, h = t.normalizeData(this.data), u = [], c = e.startAngle; this.svg = t.createSvg(this.container, e.width, e.height, e.donut ? e.classNames.chartDonut : e.classNames.chartPie), r = t.createChartRect(this.svg, e, i.padding), a = Math.min(r.width() / 2, r.height() / 2), l = e.total || h.normalized.series.reduce((function (e, t) { return e + t }), 0); var d = t.quantity(e.donutWidth); "%" === d.unit && (d.value *= a / 100), a -= e.donut && !e.donutSolid ? d.value / 2 : 0, o = "outside" === e.labelPosition || e.donut && !e.donutSolid ? a : "center" === e.labelPosition ? 0 : e.donutSolid ? a - d.value / 2 : a / 2, o += e.labelOffset; var p = { x: r.x1 + r.width() / 2, y: r.y2 + r.height() / 2 }, f = 1 === h.raw.series.filter((function (e) { return e.hasOwnProperty("value") ? 0 !== e.value : 0 !== e })).length; h.raw.series.forEach(function (e, t) { u[t] = this.svg.elem("g", null, null) }.bind(this)), e.showLabel && (s = this.svg.elem("g", null, null)), h.raw.series.forEach(function (i, r) { if (0 !== h.normalized.series[r] || !e.ignoreEmptyValues) { u[r].attr({ "ct:series-name": i.name }), u[r].addClass([e.classNames.series, i.className || e.classNames.series + "-" + t.alphaNumerate(r)].join(" ")); var m = l > 0 ? c + h.normalized.series[r] / l * 360 : 0, g = Math.max(0, c - (0 === r || f ? 0 : .2)); m - g >= 359.99 && (m = g + 359.99); var v, x, y, b = t.polarToCartesian(p.x, p.y, a, g), w = t.polarToCartesian(p.x, p.y, a, m), E = new t.Svg.Path(!e.donut || e.donutSolid).move(w.x, w.y).arc(a, a, 0, m - c > 180, 0, b.x, b.y); e.donut ? e.donutSolid && (y = a - d.value, v = t.polarToCartesian(p.x, p.y, y, c - (0 === r || f ? 0 : .2)), x = t.polarToCartesian(p.x, p.y, y, m), E.line(v.x, v.y), E.arc(y, y, 0, m - c > 180, 1, x.x, x.y)) : E.line(p.x, p.y); var S = e.classNames.slicePie; e.donut && (S = e.classNames.sliceDonut, e.donutSolid && (S = e.classNames.sliceDonutSolid)); var A = u[r].elem("path", { d: E.stringify() }, S); if (A.attr({ "ct:value": h.normalized.series[r], "ct:meta": t.serialize(i.meta) }), e.donut && !e.donutSolid && (A._node.style.strokeWidth = d.value + "px"), this.eventEmitter.emit("draw", { type: "slice", value: h.normalized.series[r], totalDataSum: l, index: r, meta: i.meta, series: i, group: u[r], element: A, path: E.clone(), center: p, radius: a, startAngle: c, endAngle: m }), e.showLabel) { var z, M; z = 1 === h.raw.series.length ? { x: p.x, y: p.y } : t.polarToCartesian(p.x, p.y, o, c + (m - c) / 2), M = h.normalized.labels && !t.isFalseyButZero(h.normalized.labels[r]) ? h.normalized.labels[r] : h.normalized.series[r]; var O = e.labelInterpolationFnc(M, r); if (O || 0 === O) { var C = s.elem("text", { dx: z.x, dy: z.y, "text-anchor": n(p, z, e.labelDirection) }, e.classNames.label).text("" + O); this.eventEmitter.emit("draw", { type: "label", index: r, group: s, element: C, text: "" + O, x: z.x, y: z.y }) } } c = m } }.bind(this)), this.eventEmitter.emit("created", { chartRect: r, svg: this.svg, options: e }) }, determineAnchorPosition: n }) }(this || global, e), e }));

var i, l, selectedLine = null;

/* Navigate to hash without browser history entry */
var navigateToHash = function () {
    if (window.history !== undefined && window.history.replaceState !== undefined) {
        window.history.replaceState(undefined, undefined, this.getAttribute("href"));
    }
};

var hashLinks = document.getElementsByClassName('navigatetohash');
for (i = 0, l = hashLinks.length; i < l; i++) {
    hashLinks[i].addEventListener('click', navigateToHash);
}

/* Switch test method */
var switchTestMethod = function () {
    var method = this.getAttribute("value");
    console.log("Selected test method: " + method);

    var lines, i, l, coverageData, lineAnalysis, cells;

    lines = document.querySelectorAll('.lineAnalysis tr');

    for (i = 1, l = lines.length; i < l; i++) {
        coverageData = JSON.parse(lines[i].getAttribute('data-coverage').replace(/'/g, '"'));
        lineAnalysis = coverageData[method];
        cells = lines[i].querySelectorAll('td');
        if (lineAnalysis === undefined) {
            lineAnalysis = coverageData.AllTestMethods;
            if (lineAnalysis.LVS !== 'gray') {
                cells[0].setAttribute('class', 'red');
                cells[1].innerText = cells[1].textContent = '0';
                cells[4].setAttribute('class', 'lightred');
            }
        } else {
            cells[0].setAttribute('class', lineAnalysis.LVS);
            cells[1].innerText = cells[1].textContent = lineAnalysis.VC;
            cells[4].setAttribute('class', 'light' + lineAnalysis.LVS);
        }
    }
};

var testMethods = document.getElementsByClassName('switchtestmethod');
for (i = 0, l = testMethods.length; i < l; i++) {
    testMethods[i].addEventListener('change', switchTestMethod);
}

/* Highlight test method by line */
var toggleLine = function () {
    if (selectedLine === this) {
        selectedLine = null;
    } else {
        selectedLine = null;
        unhighlightTestMethods();
        highlightTestMethods.call(this);
        selectedLine = this;
    }
    
};
var highlightTestMethods = function () {
    if (selectedLine !== null) {
        return;
    }

    var lineAnalysis;
    var coverageData = JSON.parse(this.getAttribute('data-coverage').replace(/'/g, '"'));
    var testMethods = document.getElementsByClassName('testmethod');

    for (i = 0, l = testMethods.length; i < l; i++) {
        lineAnalysis = coverageData[testMethods[i].id];
        if (lineAnalysis === undefined) {
            testMethods[i].className = testMethods[i].className.replace(/\s*light.+/g, "");
        } else {
            testMethods[i].className += ' light' + lineAnalysis.LVS;
        }
    }
};
var unhighlightTestMethods = function () {
    if (selectedLine !== null) {
        return;
    }

    var testMethods = document.getElementsByClassName('testmethod');
    for (i = 0, l = testMethods.length; i < l; i++) {
        testMethods[i].className = testMethods[i].className.replace(/\s*light.+/g, "");
    }
};
var coverableLines = document.getElementsByClassName('coverableline');
for (i = 0, l = coverableLines.length; i < l; i++) {
    coverableLines[i].addEventListener('click', toggleLine);
    coverableLines[i].addEventListener('mouseenter', highlightTestMethods);
    coverableLines[i].addEventListener('mouseleave', unhighlightTestMethods);
}

/* History charts */
var renderChart = function (chart) {
    // Remove current children (e.g. PNG placeholder)
    while (chart.firstChild) {
        chart.firstChild.remove();
    }

    var chartData = window[chart.getAttribute('data-data')];
    var options = {
        axisY: {
            type: undefined,
            onlyInteger: true
        },
        lineSmooth: false,
        low: 0,
        high: 100,
        scaleMinSpace: 20,
        onlyInteger: true,
        fullWidth: true
    };
    var lineChart = new Chartist.Line(chart, {
        labels: [],
        series: chartData.series
    }, options);

    /* Zoom */
    var zoomButtonDiv = document.createElement("div");
    zoomButtonDiv.className = "toggleZoom";
    var zoomButtonLink = document.createElement("a");
    zoomButtonLink.setAttribute("href", "");
    var zoomButtonText = document.createElement("i");
    zoomButtonText.className = "icon-search-plus";

    zoomButtonLink.appendChild(zoomButtonText);
    zoomButtonDiv.appendChild(zoomButtonLink);

    chart.appendChild(zoomButtonDiv);

    zoomButtonDiv.addEventListener('click', function (event) {
        event.preventDefault();

        if (options.axisY.type === undefined) {
            options.axisY.type = Chartist.AutoScaleAxis;
            zoomButtonText.className = "icon-search-minus";
        } else {
            options.axisY.type = undefined;
            zoomButtonText.className = "icon-search-plus";
        }

        lineChart.update(null, options);
    });

    var tooltip = document.createElement("div");
    tooltip.className = "tooltip";

    chart.appendChild(tooltip);

    /* Tooltips */
    var showToolTip = function () {
        var index = this.getAttribute('ct:meta');

        tooltip.innerHTML = chartData.tooltips[index];
        tooltip.style.display = 'block';
    };

    var moveToolTip = function (event) {
        var box = chart.getBoundingClientRect();
        var left = event.pageX - box.left - window.pageXOffset;
        var top = event.pageY - box.top - window.pageYOffset;

        left = left + 20;
        top = top - tooltip.offsetHeight / 2;

        if (left + tooltip.offsetWidth > box.width) {
            left -= tooltip.offsetWidth + 40;
        }

        if (top < 0) {
            top = 0;
        }

        if (top + tooltip.offsetHeight > box.height) {
            top = box.height - tooltip.offsetHeight;
        }

        tooltip.style.left = left + 'px';
        tooltip.style.top = top + 'px';
    };

    var hideToolTip = function () {
        tooltip.style.display = 'none';
    };
    chart.addEventListener('mousemove', moveToolTip);

    lineChart.on('created', function () {
        var chartPoints = chart.getElementsByClassName('ct-point');
        for (i = 0, l = chartPoints.length; i < l; i++) {
            chartPoints[i].addEventListener('mousemove', showToolTip);
            chartPoints[i].addEventListener('mouseout', hideToolTip);
        }
    });
};

var charts = document.getElementsByClassName('historychart');
for (i = 0, l = charts.length; i < l; i++) {
    renderChart(charts[i]);
}

var assemblies = [
  {
    "name": "TeamsManager.Api",
    "classes": [
      { "name": "Program", "rp": "TeamsManager.Api_Program.html", "cl": 262, "ucl": 203, "cal": 465, "tl": 619, "cb": 24, "tb": 82, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Configuration.ApiAuthConfig", "rp": "TeamsManager.Api_ApiAuthConfig.html", "cl": 26, "ucl": 7, "cal": 33, "tl": 75, "cb": 10, "tb": 14, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.AddMemberRequestDto", "rp": "TeamsManager.Api_AddMemberRequestDto.html", "cl": 2, "ucl": 0, "cal": 2, "tl": 410, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.ApplicationSettingsController", "rp": "TeamsManager.Api_ApplicationSettingsController.html", "cl": 88, "ucl": 0, "cal": 88, "tl": 187, "cb": 24, "tb": 24, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.ArchiveTeamRequestDto", "rp": "TeamsManager.Api_ArchiveTeamRequestDto.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 410, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.ArchiveTeamsRequest", "rp": "TeamsManager.Api_ArchiveTeamsRequest.html", "cl": 2, "ucl": 0, "cal": 2, "tl": 378, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.AssignTeacherToSubjectRequestDto", "rp": "TeamsManager.Api_AssignTeacherToSubjectRequestDto.html", "cl": 0, "ucl": 3, "cal": 3, "tl": 379, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.AssignUserToSchoolTypeRequestDto", "rp": "TeamsManager.Api_AssignUserToSchoolTypeRequestDto.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 379, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.AutoRepairRequest", "rp": "TeamsManager.Api_AutoRepairRequest.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 466, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.BulkArchiveRequest", "rp": "TeamsManager.Api_BulkArchiveRequest.html", "cl": 2, "ucl": 0, "cal": 2, "tl": 513, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.BulkOperationResponse", "rp": "TeamsManager.Api_BulkOperationResponse.html", "cl": 3, "ucl": 0, "cal": 3, "tl": 513, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.BulkRestoreRequest", "rp": "TeamsManager.Api_BulkRestoreRequest.html", "cl": 2, "ucl": 0, "cal": 2, "tl": 513, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.BulkRoleChangeRequest", "rp": "TeamsManager.Api_BulkRoleChangeRequest.html", "cl": 1, "ucl": 1, "cal": 2, "tl": 580, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.BulkRoleChangeResponse", "rp": "TeamsManager.Api_BulkRoleChangeResponse.html", "cl": 6, "ucl": 6, "cal": 12, "tl": 580, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.BulkTeamMembershipRequest", "rp": "TeamsManager.Api_BulkTeamMembershipRequest.html", "cl": 1, "ucl": 1, "cal": 2, "tl": 580, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.BulkTeamMembershipResponse", "rp": "TeamsManager.Api_BulkTeamMembershipResponse.html", "cl": 6, "ucl": 6, "cal": 12, "tl": 580, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.BulkUserManagementController", "rp": "TeamsManager.Api_BulkUserManagementController.html", "cl": 162, "ucl": 87, "cal": 249, "tl": 580, "cb": 55, "tb": 172, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.BulkUserOffboardingRequest", "rp": "TeamsManager.Api_BulkUserOffboardingRequest.html", "cl": 2, "ucl": 2, "cal": 4, "tl": 580, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.BulkUserOffboardingResponse", "rp": "TeamsManager.Api_BulkUserOffboardingResponse.html", "cl": 6, "ucl": 6, "cal": 12, "tl": 580, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.BulkUserOnboardingRequest", "rp": "TeamsManager.Api_BulkUserOnboardingRequest.html", "cl": 1, "ucl": 1, "cal": 2, "tl": 580, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.BulkUserOnboardingResponse", "rp": "TeamsManager.Api_BulkUserOnboardingResponse.html", "cl": 6, "ucl": 6, "cal": 12, "tl": 580, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CancelProcessResponse", "rp": "TeamsManager.Api_CancelProcessResponse.html", "cl": 2, "ucl": 2, "cal": 4, "tl": 580, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.CancelProcessResponse", "rp": "TeamsManager.Api_CancelProcessResponse.2.html", "cl": 2, "ucl": 0, "cal": 2, "tl": 378, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.ChannelsController", "rp": "TeamsManager.Api_ChannelsController.html", "cl": 45, "ucl": 58, "cal": 103, "tl": 194, "cb": 18, "tb": 36, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CloneTeamTemplateRequestDto", "rp": "TeamsManager.Api_CloneTeamTemplateRequestDto.html", "cl": 0, "ucl": 1, "cal": 1, "tl": 272, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.ComplianceReportRequest", "rp": "TeamsManager.Api_ComplianceReportRequest.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 401, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.ConsolidationRequest", "rp": "TeamsManager.Api_ConsolidationRequest.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 513, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CreateChannelRequestDto", "rp": "TeamsManager.Api_CreateChannelRequestDto.html", "cl": 3, "ucl": 0, "cal": 3, "tl": 194, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CreateDepartmentRequestDto", "rp": "TeamsManager.Api_CreateDepartmentRequestDto.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 340, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CreateSchoolTypeRequestDto", "rp": "TeamsManager.Api_CreateSchoolTypeRequestDto.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 191, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CreateSchoolYearRequestDto", "rp": "TeamsManager.Api_CreateSchoolYearRequestDto.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 209, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CreateSubjectRequestDto", "rp": "TeamsManager.Api_CreateSubjectRequestDto.html", "cl": 6, "ucl": 0, "cal": 6, "tl": 167, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CreateTeamRequestDto", "rp": "TeamsManager.Api_CreateTeamRequestDto.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 410, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.CreateTeamsForNewSchoolYearRequest", "rp": "TeamsManager.Api_CreateTeamsForNewSchoolYearRequest.html", "cl": 3, "ucl": 0, "cal": 3, "tl": 378, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CreateTeamTemplateRequestDto", "rp": "TeamsManager.Api_CreateTeamTemplateRequestDto.html", "cl": 13, "ucl": 0, "cal": 13, "tl": 272, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.CreateUserRequestDto", "rp": "TeamsManager.Api_CreateUserRequestDto.html", "cl": 15, "ucl": 0, "cal": 15, "tl": 379, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.DataImportController", "rp": "TeamsManager.Api_DataImportController.html", "cl": 90, "ucl": 0, "cal": 90, "tl": 204, "cb": 20, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.DepartmentsController", "rp": "TeamsManager.Api_DepartmentsController.html", "cl": 82, "ucl": 20, "cal": 102, "tl": 340, "cb": 19, "tb": 24, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.DiagnosticsController", "rp": "TeamsManager.Api_DiagnosticsController.html", "cl": 173, "ucl": 537, "cal": 710, "tl": 1061, "cb": 38, "tb": 162, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.GenerateTeamNameRequestDto", "rp": "TeamsManager.Api_GenerateTeamNameRequestDto.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 272, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.GraphTestRequest", "rp": "TeamsManager.Api_GraphTestRequest.html", "cl": 0, "ucl": 12, "cal": 12, "tl": 1061, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.HealthMonitoringController", "rp": "TeamsManager.Api_HealthMonitoringController.html", "cl": 190, "ucl": 24, "cal": 214, "tl": 466, "cb": 44, "tb": 50, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.OperationHistoriesController", "rp": "TeamsManager.Api_OperationHistoriesController.html", "cl": 48, "ucl": 0, "cal": 48, "tl": 113, "cb": 14, "tb": 14, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.OperationHistoryFilterDto", "rp": "TeamsManager.Api_OperationHistoryFilterDto.html", "cl": 7, "ucl": 0, "cal": 7, "tl": 113, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.ProcessStatusResponse", "rp": "TeamsManager.Api_ProcessStatusResponse.html", "cl": 3, "ucl": 0, "cal": 3, "tl": 513, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.ReportingController", "rp": "TeamsManager.Api_ReportingController.html", "cl": 109, "ucl": 40, "cal": 149, "tl": 401, "cb": 27, "tb": 52, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.SaveSettingRequestDto", "rp": "TeamsManager.Api_SaveSettingRequestDto.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 187, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.SchoolTypesController", "rp": "TeamsManager.Api_SchoolTypesController.html", "cl": 70, "ucl": 33, "cal": 103, "tl": 191, "cb": 16, "tb": 22, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.SchoolYearProcessController", "rp": "TeamsManager.Api_SchoolYearProcessController.html", "cl": 114, "ucl": 26, "cal": 140, "tl": 378, "cb": 31, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.SchoolYearReportRequest", "rp": "TeamsManager.Api_SchoolYearReportRequest.html", "cl": 2, "ucl": 0, "cal": 2, "tl": 401, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.SchoolYearsController", "rp": "TeamsManager.Api_SchoolYearsController.html", "cl": 102, "ucl": 6, "cal": 108, "tl": 209, "cb": 23, "tb": 24, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.SubjectsController", "rp": "TeamsManager.Api_SubjectsController.html", "cl": 77, "ucl": 5, "cal": 82, "tl": 167, "cb": 19, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.SystemDataExportRequest", "rp": "TeamsManager.Api_SystemDataExportRequest.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 401, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.TeamLifecycleController", "rp": "TeamsManager.Api_TeamLifecycleController.html", "cl": 205, "ucl": 42, "cal": 247, "tl": 513, "cb": 69, "tb": 120, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.TeamMigrationRequest", "rp": "TeamsManager.Api_TeamMigrationRequest.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 513, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.TeamsController", "rp": "TeamsManager.Api_TeamsController.html", "cl": 135, "ucl": 106, "cal": 241, "tl": 410, "cb": 42, "tb": 76, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.TeamTemplatesController", "rp": "TeamsManager.Api_TeamTemplatesController.html", "cl": 124, "ucl": 15, "cal": 139, "tl": 272, "cb": 35, "tb": 40, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.TestAuthController", "rp": "TeamsManager.Api_TestAuthController.html", "cl": 47, "ucl": 0, "cal": 47, "tl": 96, "cb": 17, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.API.Controllers.TransitionToNewSchoolYearRequest", "rp": "TeamsManager.Api_TransitionToNewSchoolYearRequest.html", "cl": 4, "ucl": 0, "cal": 4, "tl": 378, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UpdateChannelRequestDto", "rp": "TeamsManager.Api_UpdateChannelRequestDto.html", "cl": 2, "ucl": 0, "cal": 2, "tl": 194, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UpdateDepartmentRequestDto", "rp": "TeamsManager.Api_UpdateDepartmentRequestDto.html", "cl": 9, "ucl": 0, "cal": 9, "tl": 340, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UpdateSchoolTypeRequestDto", "rp": "TeamsManager.Api_UpdateSchoolTypeRequestDto.html", "cl": 6, "ucl": 0, "cal": 6, "tl": 191, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UpdateSchoolYearRequestDto", "rp": "TeamsManager.Api_UpdateSchoolYearRequestDto.html", "cl": 9, "ucl": 0, "cal": 9, "tl": 209, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UpdateSettingRequestDto", "rp": "TeamsManager.Api_UpdateSettingRequestDto.html", "cl": 12, "ucl": 0, "cal": 12, "tl": 187, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UpdateSubjectRequestDto", "rp": "TeamsManager.Api_UpdateSubjectRequestDto.html", "cl": 7, "ucl": 0, "cal": 7, "tl": 167, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UpdateTeamRequestDto", "rp": "TeamsManager.Api_UpdateTeamRequestDto.html", "cl": 12, "ucl": 0, "cal": 12, "tl": 410, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UpdateTeamTemplateRequestDto", "rp": "TeamsManager.Api_UpdateTeamTemplateRequestDto.html", "cl": 16, "ucl": 0, "cal": 16, "tl": 272, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UpdateUserRequestDto", "rp": "TeamsManager.Api_UpdateUserRequestDto.html", "cl": 14, "ucl": 0, "cal": 14, "tl": 379, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UserActionM365Dto", "rp": "TeamsManager.Api_UserActionM365Dto.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 379, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UserActivityReportRequest", "rp": "TeamsManager.Api_UserActivityReportRequest.html", "cl": 2, "ucl": 1, "cal": 3, "tl": 401, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Controllers.UsersController", "rp": "TeamsManager.Api_UsersController.html", "cl": 128, "ucl": 71, "cal": 199, "tl": 379, "cb": 36, "tb": 62, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Extensions.HttpContextExtensions", "rp": "TeamsManager.Api_HttpContextExtensions.html", "cl": 34, "ucl": 10, "cal": 44, "tl": 117, "cb": 18, "tb": 22, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.HealthChecks.DependencyInjectionHealthCheck", "rp": "TeamsManager.Api_DependencyInjectionHealthCheck.html", "cl": 0, "ucl": 58, "cal": 58, "tl": 85, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.HealthChecks.GraphConnectionHealthCheck", "rp": "TeamsManager.Api_GraphConnectionHealthCheck.html", "cl": 0, "ucl": 67, "cal": 67, "tl": 102, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Hubs.HubMetrics", "rp": "TeamsManager.Api_HubMetrics.html", "cl": 0, "ucl": 7, "cal": 7, "tl": 293, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Hubs.MonitoringHub", "rp": "TeamsManager.Api_MonitoringHub.html", "cl": 0, "ucl": 252, "cal": 252, "tl": 411, "cb": 0, "tb": 50, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Hubs.MonitoringHubMetrics", "rp": "TeamsManager.Api_MonitoringHubMetrics.html", "cl": 0, "ucl": 7, "cal": 7, "tl": 411, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Hubs.NotificationHub", "rp": "TeamsManager.Api_NotificationHub.html", "cl": 71, "ucl": 105, "cal": 176, "tl": 293, "cb": 16, "tb": 52, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Services.SignalRNotificationService", "rp": "TeamsManager.Api_SignalRNotificationService.html", "cl": 4, "ucl": 80, "cal": 84, "tl": 115, "cb": 1, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Swagger.AuthorizationOperationFilter", "rp": "TeamsManager.Api_AuthorizationOperationFilter.html", "cl": 0, "ucl": 104, "cal": 104, "tl": 138, "cb": 0, "tb": 22, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Swagger.ExampleSchemaFilter", "rp": "TeamsManager.Api_ExampleSchemaFilter.html", "cl": 0, "ucl": 131, "cal": 131, "tl": 154, "cb": 0, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Api.Swagger.TagsDocumentFilter", "rp": "TeamsManager.Api_TagsDocumentFilter.html", "cl": 0, "ucl": 81, "cal": 81, "tl": 111, "cb": 0, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
    ]},
  {
    "name": "TeamsManager.Application",
    "classes": [
      { "name": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "rp": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "cl": 336, "ucl": 798, "cal": 1134, "tl": 1489, "cb": 89, "tb": 252, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.DataImportOrchestrator", "rp": "TeamsManager.Application_DataImportOrchestrator.html", "cl": 66, "ucl": 407, "cal": 473, "tl": 641, "cb": 6, "tb": 100, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.HealthMonitoringOrchestrator", "rp": "TeamsManager.Application_HealthMonitoringOrchestrator.html", "cl": 391, "ucl": 57, "cal": 448, "tl": 639, "cb": 59, "tb": 88, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.ReportingOrchestrator", "rp": "TeamsManager.Application_ReportingOrchestrator.html", "cl": 540, "ucl": 25, "cal": 565, "tl": 855, "cb": 81, "tb": 99, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.SchoolYearProcessOrchestrator", "rp": "TeamsManager.Application_SchoolYearProcessOrchestrator.html", "cl": 273, "ucl": 124, "cal": 397, "tl": 581, "cb": 45, "tb": 78, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.SchoolYearReportData", "rp": "TeamsManager.Application_SchoolYearReportData.html", "cl": 6, "ucl": 0, "cal": 6, "tl": 855, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.TeamCreationPlan", "rp": "TeamsManager.Application_TeamCreationPlan.html", "cl": 4, "ucl": 0, "cal": 4, "tl": 581, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "rp": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "cl": 401, "ucl": 576, "cal": 977, "tl": 1105, "cb": 118, "tb": 398, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.TeamsManagerHealthOrchestrator", "rp": "TeamsManager.Application_TeamsManagerHealthOrchestrator.html", "cl": 289, "ucl": 409, "cal": 698, "tl": 984, "cb": 54, "tb": 130, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.UserActivityReportData", "rp": "TeamsManager.Application_UserActivityReportData.html", "cl": 6, "ucl": 0, "cal": 6, "tl": 855, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Application.Services.UserImportData", "rp": "TeamsManager.Application_UserImportData.html", "cl": 0, "ucl": 9, "cal": 9, "tl": 641, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
    ]},
  {
    "name": "TeamsManager.Core",
    "classes": [
      { "name": "TeamsManager.Core.Abstractions.ICurrentUserService", "rp": "TeamsManager.Core_ICurrentUserService.html", "cl": 0, "ucl": 1, "cal": 1, "tl": 32, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ArchiveOptions", "rp": "TeamsManager.Core_ArchiveOptions.html", "cl": 10, "ucl": 0, "cal": 10, "tl": 281, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.Auth.TokenInfo", "rp": "TeamsManager.Core_TokenInfo.html", "cl": 4, "ucl": 0, "cal": 4, "tl": 66, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ConsolidationOptions", "rp": "TeamsManager.Core_ConsolidationOptions.html", "cl": 6, "ucl": 1, "cal": 7, "tl": 281, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.DateRange", "rp": "TeamsManager.Core_DateRange.html", "cl": 0, "ucl": 3, "cal": 3, "tl": 396, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ExportOptions", "rp": "TeamsManager.Core_ExportOptions.html", "cl": 6, "ucl": 2, "cal": 8, "tl": 396, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.Graph.GraphTokenInfo", "rp": "TeamsManager.Core_GraphTokenInfo.html", "cl": 0, "ucl": 6, "cal": 6, "tl": 86, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ImportOptions", "rp": "TeamsManager.Core_ImportOptions.html", "cl": 14, "ucl": 1, "cal": 15, "tl": 324, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ImportProcessStatus", "rp": "TeamsManager.Core_ImportProcessStatus.html", "cl": 14, "ucl": 3, "cal": 17, "tl": 324, "cb": 1, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ImportValidationError", "rp": "TeamsManager.Core_ImportValidationError.html", "cl": 5, "ucl": 1, "cal": 6, "tl": 324, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ImportValidationResult", "rp": "TeamsManager.Core_ImportValidationResult.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 324, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ImportValidationWarning", "rp": "TeamsManager.Core_ImportValidationWarning.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 324, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.OffboardingOptions", "rp": "TeamsManager.Core_OffboardingOptions.html", "cl": 8, "ucl": 1, "cal": 9, "tl": 332, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.RepairOptions", "rp": "TeamsManager.Core_RepairOptions.html", "cl": 9, "ucl": 0, "cal": 9, "tl": 109, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ReportingProcessStatus", "rp": "TeamsManager.Core_ReportingProcessStatus.html", "cl": 12, "ucl": 1, "cal": 13, "tl": 396, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ReportOperationResult", "rp": "TeamsManager.Core_ReportOperationResult.html", "cl": 26, "ucl": 5, "cal": 31, "tl": 396, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.ReportOptions", "rp": "TeamsManager.Core_ReportOptions.html", "cl": 8, "ucl": 1, "cal": 9, "tl": 396, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.RestoreOptions", "rp": "TeamsManager.Core_RestoreOptions.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 281, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.SchoolYearProcessOptions", "rp": "TeamsManager.Core_SchoolYearProcessOptions.html", "cl": 8, "ucl": 3, "cal": 11, "tl": 151, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.SchoolYearProcessStatus", "rp": "TeamsManager.Core_SchoolYearProcessStatus.html", "cl": 13, "ucl": 1, "cal": 14, "tl": 151, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.TeamLifecycleProcessStatus", "rp": "TeamsManager.Core_TeamLifecycleProcessStatus.html", "cl": 12, "ucl": 2, "cal": 14, "tl": 281, "cb": 1, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.TeamMembershipOperation", "rp": "TeamsManager.Core_TeamMembershipOperation.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 332, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.TeamMigrationPlan", "rp": "TeamsManager.Core_TeamMigrationPlan.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 281, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.UserManagementProcessStatus", "rp": "TeamsManager.Core_UserManagementProcessStatus.html", "cl": 11, "ucl": 3, "cal": 14, "tl": 332, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.UserOnboardingPlan", "rp": "TeamsManager.Core_UserOnboardingPlan.html", "cl": 10, "ucl": 0, "cal": 10, "tl": 332, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Abstractions.Services.UserRoleChange", "rp": "TeamsManager.Core_UserRoleChange.html", "cl": 6, "ucl": 0, "cal": 6, "tl": 332, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Common.CircuitBreaker", "rp": "TeamsManager.Core_CircuitBreaker.html", "cl": 64, "ucl": 9, "cal": 73, "tl": 168, "cb": 19, "tb": 22, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Common.CircuitBreakerFailureEventArgs", "rp": "TeamsManager.Core_CircuitBreakerFailureEventArgs.html", "cl": 8, "ucl": 1, "cal": 9, "tl": 168, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Common.CircuitBreakerOpenException", "rp": "TeamsManager.Core_CircuitBreakerOpenException.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 168, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Common.CircuitBreakerStateChangedEventArgs", "rp": "TeamsManager.Core_CircuitBreakerStateChangedEventArgs.html", "cl": 8, "ucl": 1, "cal": 9, "tl": 168, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Common.CircuitBreakerStats", "rp": "TeamsManager.Core_CircuitBreakerStats.html", "cl": 0, "ucl": 6, "cal": 6, "tl": 182, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Common.ModernCircuitBreaker", "rp": "TeamsManager.Core_ModernCircuitBreaker.html", "cl": 36, "ucl": 63, "cal": 99, "tl": 182, "cb": 6, "tb": 38, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "rp": "TeamsManager.Core_GraphApiException.html", "cl": 0, "ucl": 173, "cal": 173, "tl": 390, "cb": 0, "tb": 96, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Exceptions.Graph.GraphConnectionException", "rp": "TeamsManager.Core_GraphConnectionException.html", "cl": 36, "ucl": 63, "cal": 99, "tl": 237, "cb": 19, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Exceptions.Graph.GraphRateLimitException", "rp": "TeamsManager.Core_GraphRateLimitException.html", "cl": 0, "ucl": 182, "cal": 182, "tl": 433, "cb": 0, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Exceptions.Graph.GraphValidationException", "rp": "TeamsManager.Core_GraphValidationException.html", "cl": 0, "ucl": 166, "cal": 166, "tl": 429, "cb": 0, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Exceptions.Graph.ValidationError", "rp": "TeamsManager.Core_ValidationError.html", "cl": 0, "ucl": 19, "cal": 19, "tl": 429, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Extensions.EnumExtensions", "rp": "TeamsManager.Core_EnumExtensions.html", "cl": 16, "ucl": 120, "cal": 136, "tl": 182, "cb": 32, "tb": 250, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Extensions.GraphServiceExtensions", "rp": "TeamsManager.Core_GraphServiceExtensions.html", "cl": 19, "ucl": 3, "cal": 22, "tl": 72, "cb": 6, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Helpers.AuditHelper", "rp": "TeamsManager.Core_AuditHelper.html", "cl": 0, "ucl": 14, "cal": 14, "tl": 84, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Helpers.GraphModelMapper", "rp": "TeamsManager.Core_GraphModelMapper.html", "cl": 0, "ucl": 70, "cal": 70, "tl": 133, "cb": 0, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.ApplicationSetting", "rp": "TeamsManager.Core_ApplicationSetting.html", "cl": 85, "ucl": 3, "cal": 88, "tl": 233, "cb": 24, "tb": 24, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.BaseEntity", "rp": "TeamsManager.Core_BaseEntity.html", "cl": 14, "ucl": 0, "cal": 14, "tl": 44, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.BulkOperationError", "rp": "TeamsManager.Core_BulkOperationError.html", "cl": 6, "ucl": 0, "cal": 6, "tl": 172, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.BulkOperationResult", "rp": "TeamsManager.Core_BulkOperationResult.html", "cl": 33, "ucl": 0, "cal": 33, "tl": 172, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.BulkOperationSuccess", "rp": "TeamsManager.Core_BulkOperationSuccess.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 172, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Channel", "rp": "TeamsManager.Core_Channel.html", "cl": 50, "ucl": 75, "cal": 125, "tl": 377, "cb": 13, "tb": 62, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Department", "rp": "TeamsManager.Core_Department.html", "cl": 142, "ucl": 9, "cal": 151, "tl": 368, "cb": 59, "tb": 64, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.BulkOperationProgress", "rp": "TeamsManager.Core_BulkOperationProgress.html", "cl": 48, "ucl": 0, "cal": 48, "tl": 245, "cb": 30, "tb": 30, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.CacheExpiryInfo", "rp": "TeamsManager.Core_CacheExpiryInfo.html", "cl": 0, "ucl": 9, "cal": 9, "tl": 799, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphApiAvailability", "rp": "TeamsManager.Core_GraphApiAvailability.html", "cl": 0, "ucl": 14, "cal": 14, "tl": 1143, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphApiConfiguration", "rp": "TeamsManager.Core_GraphApiConfiguration.html", "cl": 6, "ucl": 0, "cal": 6, "tl": 146, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphApiError", "rp": "TeamsManager.Core_GraphApiError.html", "cl": 0, "ucl": 9, "cal": 9, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphAttachment", "rp": "TeamsManager.Core_GraphAttachment.html", "cl": 0, "ucl": 7, "cal": 7, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphBatchOperation", "rp": "TeamsManager.Core_GraphBatchOperation.html", "cl": 38, "ucl": 38, "cal": 76, "tl": 245, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphBatchOperationResult", "rp": "TeamsManager.Core_GraphBatchOperationResult.html", "cl": 5, "ucl": 6, "cal": 11, "tl": 553, "cb": 1, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphBatchRequest", "rp": "TeamsManager.Core_GraphBatchRequest.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphBatchRequestItem", "rp": "TeamsManager.Core_GraphBatchRequestItem.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphBatchResponse", "rp": "TeamsManager.Core_GraphBatchResponse.html", "cl": 2, "ucl": 2, "cal": 4, "tl": 1143, "cb": 1, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphBatchResponseItem", "rp": "TeamsManager.Core_GraphBatchResponseItem.html", "cl": 4, "ucl": 0, "cal": 4, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphBulkOperationError", "rp": "TeamsManager.Core_GraphBulkOperationError.html", "cl": 3, "ucl": 9, "cal": 12, "tl": 553, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphBulkOperationSuccess", "rp": "TeamsManager.Core_GraphBulkOperationSuccess.html", "cl": 5, "ucl": 4, "cal": 9, "tl": 553, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphBulkResult", "rp": "TeamsManager.Core_GraphBulkResult.html", "cl": 105, "ucl": 53, "cal": 158, "tl": 553, "cb": 40, "tb": 70, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphCacheConfiguration", "rp": "TeamsManager.Core_GraphCacheConfiguration.html", "cl": 19, "ucl": 0, "cal": 19, "tl": 295, "cb": 5, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphCacheInfo", "rp": "TeamsManager.Core_GraphCacheInfo.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphCacheMetadata", "rp": "TeamsManager.Core_GraphCacheMetadata.html", "cl": 6, "ucl": 4, "cal": 10, "tl": 799, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphCacheMetrics", "rp": "TeamsManager.Core_GraphCacheMetrics.html", "cl": 40, "ucl": 2, "cal": 42, "tl": 799, "cb": 9, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphCacheRateLimitInfo", "rp": "TeamsManager.Core_GraphCacheRateLimitInfo.html", "cl": 5, "ucl": 1, "cal": 6, "tl": 799, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphCacheValidationResult", "rp": "TeamsManager.Core_GraphCacheValidationResult.html", "cl": 15, "ucl": 21, "cal": 36, "tl": 799, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphCacheWarmupOptions", "rp": "TeamsManager.Core_GraphCacheWarmupOptions.html", "cl": 4, "ucl": 0, "cal": 4, "tl": 740, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphCacheWarmupResult", "rp": "TeamsManager.Core_GraphCacheWarmupResult.html", "cl": 5, "ucl": 5, "cal": 10, "tl": 740, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphChannel", "rp": "TeamsManager.Core_GraphChannel.html", "cl": 134, "ucl": 0, "cal": 134, "tl": 488, "cb": 68, "tb": 76, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphChannelMember", "rp": "TeamsManager.Core_GraphChannelMember.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 488, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphChannelSettings", "rp": "TeamsManager.Core_GraphChannelSettings.html", "cl": 9, "ucl": 0, "cal": 9, "tl": 488, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphChannelStats", "rp": "TeamsManager.Core_GraphChannelStats.html", "cl": 7, "ucl": 0, "cal": 7, "tl": 488, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphChannelTab", "rp": "TeamsManager.Core_GraphChannelTab.html", "cl": 9, "ucl": 0, "cal": 9, "tl": 488, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphConnectionHealthInfo", "rp": "TeamsManager.Core_GraphConnectionHealthInfo.html", "cl": 7, "ucl": 2, "cal": 9, "tl": 1143, "cb": 2, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphConnectionTestResult", "rp": "TeamsManager.Core_GraphConnectionTestResult.html", "cl": 10, "ucl": 9, "cal": 19, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphDiagnosticInfo", "rp": "TeamsManager.Core_GraphDiagnosticInfo.html", "cl": 12, "ucl": 8, "cal": 20, "tl": 1143, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphEmailAddress", "rp": "TeamsManager.Core_GraphEmailAddress.html", "cl": 0, "ucl": 2, "cal": 2, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphEndpointCacheMetrics", "rp": "TeamsManager.Core_GraphEndpointCacheMetrics.html", "cl": 14, "ucl": 1, "cal": 15, "tl": 799, "cb": 2, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphEndpoints", "rp": "TeamsManager.Core_GraphEndpoints.html", "cl": 3, "ucl": 28, "cal": 31, "tl": 146, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphLicense", "rp": "TeamsManager.Core_GraphLicense.html", "cl": 3, "ucl": 3, "cal": 6, "tl": 428, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphMessage", "rp": "TeamsManager.Core_GraphMessage.html", "cl": 0, "ucl": 15, "cal": 15, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphMessageBody", "rp": "TeamsManager.Core_GraphMessageBody.html", "cl": 0, "ucl": 2, "cal": 2, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphMessagesResponse", "rp": "TeamsManager.Core_GraphMessagesResponse.html", "cl": 0, "ucl": 3, "cal": 3, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphMetricsInfo", "rp": "TeamsManager.Core_GraphMetricsInfo.html", "cl": 0, "ucl": 15, "cal": 15, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphOperationError", "rp": "TeamsManager.Core_GraphOperationError.html", "cl": 5, "ucl": 7, "cal": 12, "tl": 740, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphOperationResult<T>", "rp": "TeamsManager.Core_GraphOperationResult_1.html", "cl": 124, "ucl": 126, "cal": 250, "tl": 740, "cb": 28, "tb": 84, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphOperationSuccess", "rp": "TeamsManager.Core_GraphOperationSuccess.html", "cl": 0, "ucl": 9, "cal": 9, "tl": 740, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphPermissionInfo", "rp": "TeamsManager.Core_GraphPermissionInfo.html", "cl": 10, "ucl": 20, "cal": 30, "tl": 1143, "cb": 2, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphPermissionScopes", "rp": "TeamsManager.Core_GraphPermissionScopes.html", "cl": 0, "ucl": 24, "cal": 24, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphRateLimitConfiguration", "rp": "TeamsManager.Core_GraphRateLimitConfiguration.html", "cl": 16, "ucl": 0, "cal": 16, "tl": 295, "cb": 3, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphRateLimitStatus", "rp": "TeamsManager.Core_GraphRateLimitStatus.html", "cl": 7, "ucl": 6, "cal": 13, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphRetryConfiguration", "rp": "TeamsManager.Core_GraphRetryConfiguration.html", "cl": 18, "ucl": 0, "cal": 18, "tl": 295, "cb": 4, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphScopes", "rp": "TeamsManager.Core_GraphScopes.html", "cl": 10, "ucl": 18, "cal": 28, "tl": 146, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphSendMailRequest", "rp": "TeamsManager.Core_GraphSendMailRequest.html", "cl": 0, "ucl": 2, "cal": 2, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphServiceConfiguration", "rp": "TeamsManager.Core_GraphServiceConfiguration.html", "cl": 47, "ucl": 0, "cal": 47, "tl": 295, "cb": 8, "tb": 16, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphServiceMetrics", "rp": "TeamsManager.Core_GraphServiceMetrics.html", "cl": 5, "ucl": 1, "cal": 6, "tl": 740, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphServicePlan", "rp": "TeamsManager.Core_GraphServicePlan.html", "cl": 4, "ucl": 1, "cal": 5, "tl": 428, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphSyncInfo", "rp": "TeamsManager.Core_GraphSyncInfo.html", "cl": 11, "ucl": 0, "cal": 11, "tl": 472, "cb": 4, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphTeam", "rp": "TeamsManager.Core_GraphTeam.html", "cl": 65, "ucl": 0, "cal": 65, "tl": 472, "cb": 12, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphTeamDiscoverySettings", "rp": "TeamsManager.Core_GraphTeamDiscoverySettings.html", "cl": 1, "ucl": 0, "cal": 1, "tl": 472, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphTeamFunSettings", "rp": "TeamsManager.Core_GraphTeamFunSettings.html", "cl": 4, "ucl": 0, "cal": 4, "tl": 472, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphTeamGuestSettings", "rp": "TeamsManager.Core_GraphTeamGuestSettings.html", "cl": 2, "ucl": 0, "cal": 2, "tl": 472, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphTeamMember", "rp": "TeamsManager.Core_GraphTeamMember.html", "cl": 9, "ucl": 0, "cal": 9, "tl": 472, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphTeamMemberSettings", "rp": "TeamsManager.Core_GraphTeamMemberSettings.html", "cl": 3, "ucl": 0, "cal": 3, "tl": 472, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphTeamMessagingSettings", "rp": "TeamsManager.Core_GraphTeamMessagingSettings.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 472, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphTeamSettings", "rp": "TeamsManager.Core_GraphTeamSettings.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 472, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphTestResult", "rp": "TeamsManager.Core_GraphTestResult.html", "cl": 0, "ucl": 4, "cal": 4, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphUser", "rp": "TeamsManager.Core_GraphUser.html", "cl": 122, "ucl": 0, "cal": 122, "tl": 428, "cb": 38, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GraphUserContext", "rp": "TeamsManager.Core_GraphUserContext.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 1143, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.GroupMetadata", "rp": "TeamsManager.Core_GroupMetadata.html", "cl": 3, "ucl": 11, "cal": 14, "tl": 799, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.License", "rp": "TeamsManager.Core_License.html", "cl": 0, "ucl": 13, "cal": 13, "tl": 62, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.OperationResult<T>", "rp": "TeamsManager.Core_OperationResult_1.html", "cl": 4, "ucl": 0, "cal": 4, "tl": 740, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.TeamGroupCacheStats", "rp": "TeamsManager.Core_TeamGroupCacheStats.html", "cl": 5, "ucl": 12, "cal": 17, "tl": 799, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.TeamMetadata", "rp": "TeamsManager.Core_TeamMetadata.html", "cl": 3, "ucl": 11, "cal": 14, "tl": 799, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.TtlStats", "rp": "TeamsManager.Core_TtlStats.html", "cl": 9, "ucl": 14, "cal": 23, "tl": 799, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Graph.UserIdCacheStats", "rp": "TeamsManager.Core_UserIdCacheStats.html", "cl": 2, "ucl": 8, "cal": 10, "tl": 799, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.HealthCheckDetail", "rp": "TeamsManager.Core_HealthCheckDetail.html", "cl": 6, "ucl": 0, "cal": 6, "tl": 333, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.HealthMetrics", "rp": "TeamsManager.Core_HealthMetrics.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 333, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.HealthMonitoringProcessStatus", "rp": "TeamsManager.Core_HealthMonitoringProcessStatus.html", "cl": 13, "ucl": 0, "cal": 13, "tl": 333, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.HealthOperationError", "rp": "TeamsManager.Core_HealthOperationError.html", "cl": 7, "ucl": 0, "cal": 7, "tl": 333, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.HealthOperationResult", "rp": "TeamsManager.Core_HealthOperationResult.html", "cl": 33, "ucl": 0, "cal": 33, "tl": 333, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.HealthOperationSuccess", "rp": "TeamsManager.Core_HealthOperationSuccess.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 333, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.OperationHistory", "rp": "TeamsManager.Core_OperationHistory.html", "cl": 91, "ucl": 7, "cal": 98, "tl": 290, "cb": 33, "tb": 40, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.OrganizationalUnit", "rp": "TeamsManager.Core_OrganizationalUnit.html", "cl": 30, "ucl": 0, "cal": 30, "tl": 105, "cb": 4, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.ProcessCancelResponse", "rp": "TeamsManager.Core_ProcessCancelResponse.html", "cl": 25, "ucl": 0, "cal": 25, "tl": 144, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.ProcessStatusResponse<T>", "rp": "TeamsManager.Core_ProcessStatusResponse_1.html", "cl": 0, "ucl": 23, "cal": 23, "tl": 144, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.SchoolType", "rp": "TeamsManager.Core_SchoolType.html", "cl": 25, "ucl": 0, "cal": 25, "tl": 100, "cb": 17, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.SchoolYear", "rp": "TeamsManager.Core_SchoolYear.html", "cl": 35, "ucl": 2, "cal": 37, "tl": 145, "cb": 29, "tb": 34, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Subject", "rp": "TeamsManager.Core_Subject.html", "cl": 8, "ucl": 0, "cal": 8, "tl": 54, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.Team", "rp": "TeamsManager.Core_Team.html", "cl": 146, "ucl": 5, "cal": 151, "tl": 509, "cb": 119, "tb": 130, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.TeamMember", "rp": "TeamsManager.Core_TeamMember.html", "cl": 19, "ucl": 91, "cal": 110, "tl": 365, "cb": 12, "tb": 56, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.TeamTemplate", "rp": "TeamsManager.Core_TeamTemplate.html", "cl": 137, "ucl": 16, "cal": 153, "tl": 363, "cb": 35, "tb": 50, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.User", "rp": "TeamsManager.Core_User.html", "cl": 98, "ucl": 8, "cal": 106, "tl": 323, "cb": 74, "tb": 86, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.UserSchoolType", "rp": "TeamsManager.Core_UserSchoolType.html", "cl": 18, "ucl": 0, "cal": 18, "tl": 92, "cb": 14, "tb": 14, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Models.UserSubject", "rp": "TeamsManager.Core_UserSubject.html", "cl": 6, "ucl": 0, "cal": 6, "tl": 37, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.ApplicationSettingService", "rp": "TeamsManager.Core_ApplicationSettingService.html", "cl": 15, "ucl": 375, "cal": 390, "tl": 587, "cb": 6, "tb": 123, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Auth.TokenManager", "rp": "TeamsManager.Core_TokenManager.html", "cl": 95, "ucl": 36, "cal": 131, "tl": 209, "cb": 44, "tb": 64, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Cache.CacheInvalidationService", "rp": "TeamsManager.Core_CacheInvalidationService.html", "cl": 0, "ucl": 357, "cal": 357, "tl": 586, "cb": 0, "tb": 84, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Cache.CascadeInvalidationStrategy", "rp": "TeamsManager.Core_CascadeInvalidationStrategy.html", "cl": 0, "ucl": 55, "cal": 55, "tl": 586, "cb": 0, "tb": 16, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Cache.CascadeRule", "rp": "TeamsManager.Core_CascadeRule.html", "cl": 0, "ucl": 7, "cal": 7, "tl": 586, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Cache.TeamTemplateCacheKeys", "rp": "TeamsManager.Core_TeamTemplateCacheKeys.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 43, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.ChannelService", "rp": "TeamsManager.Core_ChannelService.html", "cl": 359, "ucl": 217, "cal": 576, "tl": 804, "cb": 134, "tb": 228, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.DepartmentService", "rp": "TeamsManager.Core_DepartmentService.html", "cl": 254, "ucl": 170, "cal": 424, "tl": 651, "cb": 66, "tb": 114, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "rp": "TeamsManager.Core_GraphBulkOperationsService.html", "cl": 349, "ucl": 1504, "cal": 1853, "tl": 2143, "cb": 108, "tb": 988, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Graph.GraphCacheService", "rp": "TeamsManager.Core_GraphCacheService.html", "cl": 527, "ucl": 958, "cal": 1485, "tl": 1949, "cb": 136, "tb": 394, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Graph.GraphConnectionService", "rp": "TeamsManager.Core_GraphConnectionService.html", "cl": 77, "ucl": 859, "cal": 936, "tl": 1345, "cb": 11, "tb": 338, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Graph.GraphExceptionHandler", "rp": "TeamsManager.Core_GraphExceptionHandler.html", "cl": 7, "ucl": 366, "cal": 373, "tl": 636, "cb": 6, "tb": 124, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Graph.GraphService", "rp": "TeamsManager.Core_GraphService.html", "cl": 304, "ucl": 194, "cal": 498, "tl": 877, "cb": 67, "tb": 96, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "rp": "TeamsManager.Core_GraphTeamManagementService.html", "cl": 66, "ucl": 1549, "cal": 1615, "tl": 1899, "cb": 20, "tb": 2110, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Graph.GraphTokenManager", "rp": "TeamsManager.Core_GraphTokenManager.html", "cl": 0, "ucl": 117, "cal": 117, "tl": 226, "cb": 0, "tb": 36, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "rp": "TeamsManager.Core_GraphUserManagementService.html", "cl": 49, "ucl": 1114, "cal": 1163, "tl": 1854, "cb": 21, "tb": 334, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.GraphAdminNotificationService", "rp": "TeamsManager.Core_GraphAdminNotificationService.html", "cl": 0, "ucl": 313, "cal": 313, "tl": 469, "cb": 0, "tb": 74, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.ModernHttpService", "rp": "TeamsManager.Core_ModernHttpService.html", "cl": 84, "ucl": 516, "cal": 600, "tl": 1152, "cb": 18, "tb": 100, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.OperationHistoryService", "rp": "TeamsManager.Core_OperationHistoryService.html", "cl": 61, "ucl": 86, "cal": 147, "tl": 268, "cb": 26, "tb": 62, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.OrganizationalUnitService", "rp": "TeamsManager.Core_OrganizationalUnitService.html", "cl": 0, "ucl": 675, "cal": 675, "tl": 1021, "cb": 0, "tb": 180, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.PredicateBuilder", "rp": "TeamsManager.Core_PredicateBuilder.html", "cl": 0, "ucl": 12, "cal": 12, "tl": 268, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.SchoolTypeService", "rp": "TeamsManager.Core_SchoolTypeService.html", "cl": 17, "ucl": 377, "cal": 394, "tl": 614, "cb": 7, "tb": 88, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.SchoolYearService", "rp": "TeamsManager.Core_SchoolYearService.html", "cl": 350, "ucl": 191, "cal": 541, "tl": 816, "cb": 86, "tb": 134, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.SeedDataService", "rp": "TeamsManager.Core_SeedDataService.html", "cl": 0, "ucl": 99, "cal": 99, "tl": 179, "cb": 0, "tb": 22, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.StubAdminNotificationService", "rp": "TeamsManager.Core_StubAdminNotificationService.html", "cl": 0, "ucl": 42, "cal": 42, "tl": 74, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.StubNotificationService", "rp": "TeamsManager.Core_StubNotificationService.html", "cl": 0, "ucl": 54, "cal": 54, "tl": 91, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.SubjectService", "rp": "TeamsManager.Core_SubjectService.html", "cl": 27, "ucl": 392, "cal": 419, "tl": 632, "cb": 10, "tb": 104, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Synchronization.ChannelSynchronizer", "rp": "TeamsManager.Core_ChannelSynchronizer.html", "cl": 0, "ucl": 96, "cal": 96, "tl": 167, "cb": 0, "tb": 78, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Synchronization.GraphSynchronizerBase<T1, T2>", "rp": "TeamsManager.Core_GraphSynchronizerBase_2.html", "cl": 0, "ucl": 101, "cal": 101, "tl": 204, "cb": 0, "tb": 60, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Synchronization.TeamSynchronizer", "rp": "TeamsManager.Core_TeamSynchronizer.html", "cl": 0, "ucl": 167, "cal": 167, "tl": 280, "cb": 0, "tb": 82, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.Synchronization.UserSynchronizer", "rp": "TeamsManager.Core_UserSynchronizer.html", "cl": 0, "ucl": 90, "cal": 90, "tl": 173, "cb": 0, "tb": 42, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.TeamService", "rp": "TeamsManager.Core_TeamService.html", "cl": 296, "ucl": 1392, "cal": 1688, "tl": 2331, "cb": 94, "tb": 590, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.TeamTemplateService", "rp": "TeamsManager.Core_TeamTemplateService.html", "cl": 20, "ucl": 607, "cal": 627, "tl": 933, "cb": 8, "tb": 164, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.UserContext.CurrentUserService", "rp": "TeamsManager.Core_CurrentUserService.html", "cl": 45, "ucl": 9, "cal": 54, "tl": 111, "cb": 38, "tb": 56, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Services.UserService", "rp": "TeamsManager.Core_UserService.html", "cl": 378, "ucl": 859, "cal": 1237, "tl": 1721, "cb": 92, "tb": 310, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Validation.OrganizationalUnitValidator", "rp": "TeamsManager.Core_OrganizationalUnitValidator.html", "cl": 134, "ucl": 107, "cal": 241, "tl": 439, "cb": 56, "tb": 92, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Core.Validation.ValidationResult", "rp": "TeamsManager.Core_ValidationResult.html", "cl": 13, "ucl": 12, "cal": 25, "tl": 439, "cb": 2, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
    ]},
  {
    "name": "TeamsManager.Data",
    "classes": [
      { "name": "TeamsManager.Data.DesignTimeDbContextFactory", "rp": "TeamsManager.Data_DesignTimeDbContextFactory.html", "cl": 9, "ucl": 3, "cal": 12, "tl": 35, "cb": 1, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.AddOrganizationalUnit", "rp": "TeamsManager.Data_AddOrganizationalUnit.html", "cl": 1513, "ucl": 13, "cal": 1526, "tl": 1610, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.AddOrganizationalUnitCode", "rp": "TeamsManager.Data_AddOrganizationalUnitCode.html", "cl": 1462, "ucl": 5, "cal": 1467, "tl": 1539, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.AddPracownikAdministracyjnyRole", "rp": "TeamsManager.Data_AddPracownikAdministracyjnyRole.html", "cl": 1469, "ucl": 2, "cal": 1471, "tl": 1545, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.AddSystemDefaultFlags", "rp": "TeamsManager.Data_AddSystemDefaultFlags.html", "cl": 1489, "ucl": 14, "cal": 1503, "tl": 1581, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.AddTeamDepartmentRelation", "rp": "TeamsManager.Data_AddTeamDepartmentRelation.html", "cl": 1388, "ucl": 11, "cal": 1399, "tl": 1472, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.FixPendingChanges", "rp": "TeamsManager.Data_FixPendingChanges.html", "cl": 1364, "ucl": 5, "cal": 1369, "tl": 1438, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.InitialCreate", "rp": "TeamsManager.Data_InitialCreate.html", "cl": 2189, "ucl": 30, "cal": 2219, "tl": 2404, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.RemoveCodeLengthLimit", "rp": "TeamsManager.Data_RemoveCodeLengthLimit.html", "cl": 1455, "ucl": 2, "cal": 1457, "tl": 1531, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.ReplaceTeamIsVisibleWithVisibility", "rp": "TeamsManager.Data_ReplaceTeamIsVisibleWithVisibility.html", "cl": 1359, "ucl": 6, "cal": 1365, "tl": 1434, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Migrations.TeamsManagerDbContextModelSnapshot", "rp": "TeamsManager.Data_TeamsManagerDbContextModelSnapshot.html", "cl": 1467, "ucl": 0, "cal": 1467, "tl": 1520, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Program", "rp": "TeamsManager.Data_Program.html", "cl": 0, "ucl": 220, "cal": 220, "tl": 308, "cb": 0, "tb": 46, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Repositories.ApplicationSettingRepository", "rp": "TeamsManager.Data_ApplicationSettingRepository.html", "cl": 9, "ucl": 0, "cal": 9, "tl": 26, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Repositories.GenericRepository<T>", "rp": "TeamsManager.Data_GenericRepository_1.html", "cl": 47, "ucl": 0, "cal": 47, "tl": 103, "cb": 5, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Repositories.OperationHistoryRepository", "rp": "TeamsManager.Data_OperationHistoryRepository.html", "cl": 33, "ucl": 0, "cal": 33, "tl": 61, "cb": 8, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Repositories.SchoolYearRepository", "rp": "TeamsManager.Data_SchoolYearRepository.html", "cl": 15, "ucl": 0, "cal": 15, "tl": 35, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Repositories.SubjectRepository", "rp": "TeamsManager.Data_SubjectRepository.html", "cl": 36, "ucl": 6, "cal": 42, "tl": 128, "cb": 1, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Repositories.TeamRepository", "rp": "TeamsManager.Data_TeamRepository.html", "cl": 49, "ucl": 2, "cal": 51, "tl": 95, "cb": 2, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Repositories.TeamTemplateRepository", "rp": "TeamsManager.Data_TeamTemplateRepository.html", "cl": 33, "ucl": 3, "cal": 36, "tl": 66, "cb": 2, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.Repositories.UserRepository", "rp": "TeamsManager.Data_UserRepository.html", "cl": 56, "ucl": 1, "cal": 57, "tl": 104, "cb": 3, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.TeamsManagerDbContext", "rp": "TeamsManager.Data_TeamsManagerDbContext.html", "cl": 524, "ucl": 4, "cal": 528, "tl": 614, "cb": 16, "tb": 16, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.TestDataSeeder", "rp": "TeamsManager.Data_TestDataSeeder.html", "cl": 333, "ucl": 0, "cal": 333, "tl": 398, "cb": 2, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.Data.UnitOfWork.EfUnitOfWork", "rp": "TeamsManager.Data_EfUnitOfWork.html", "cl": 72, "ucl": 51, "cal": 123, "tl": 233, "cb": 30, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
    ]},
  {
    "name": "TeamsManager.UI",
    "classes": [
      { "name": "TeamsManager.UI.App", "rp": "TeamsManager.UI_App.html", "cl": 0, "ucl": 444, "cal": 444, "tl": 1058, "cb": 0, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.AlertLevelToColorConverter", "rp": "TeamsManager.UI_AlertLevelToColorConverter.html", "cl": 0, "ucl": 15, "cal": 15, "tl": 142, "cb": 0, "tb": 9, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.BooleanToOpacityConverter", "rp": "TeamsManager.UI_BooleanToOpacityConverter.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 28, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.BooleanToVisibilityConverter", "rp": "TeamsManager.UI_BooleanToVisibilityConverter.html", "cl": 0, "ucl": 17, "cal": 17, "tl": 41, "cb": 0, "tb": 18, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.BooleanToYesNoConverter", "rp": "TeamsManager.UI_BooleanToYesNoConverter.html", "cl": 0, "ucl": 12, "cal": 12, "tl": 30, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.BoolToBackgroundConverter", "rp": "TeamsManager.UI_BoolToBackgroundConverter.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 31, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.BoolToIconConverter", "rp": "TeamsManager.UI_BoolToIconConverter.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 24, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.ColorToBrushConverter", "rp": "TeamsManager.UI_ColorToBrushConverter.html", "cl": 0, "ucl": 20, "cal": 20, "tl": 45, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.ConnectionStateToColorConverter", "rp": "TeamsManager.UI_ConnectionStateToColorConverter.html", "cl": 0, "ucl": 16, "cal": 16, "tl": 142, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.DateTimeToRelativeConverter", "rp": "TeamsManager.UI_DateTimeToRelativeConverter.html", "cl": 0, "ucl": 24, "cal": 24, "tl": 181, "cb": 0, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.EnumDescriptionConverter", "rp": "TeamsManager.UI_EnumDescriptionConverter.html", "cl": 0, "ucl": 43, "cal": 43, "tl": 86, "cb": 0, "tb": 40, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.EqualToVisibilityConverter", "rp": "TeamsManager.UI_EqualToVisibilityConverter.html", "cl": 0, "ucl": 7, "cal": 7, "tl": 26, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.GreaterThanConverter", "rp": "TeamsManager.UI_GreaterThanConverter.html", "cl": 0, "ucl": 17, "cal": 17, "tl": 41, "cb": 0, "tb": 18, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.HealthCheckToColorConverter", "rp": "TeamsManager.UI_HealthCheckToColorConverter.html", "cl": 0, "ucl": 14, "cal": 14, "tl": 142, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.HealthCheckToPolishConverter", "rp": "TeamsManager.UI_HealthCheckToPolishConverter.html", "cl": 0, "ucl": 15, "cal": 15, "tl": 35, "cb": 0, "tb": 13, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.HierarchyLevelToMarginConverter", "rp": "TeamsManager.UI_HierarchyLevelToMarginConverter.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 31, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.IntToVisibilityConverter", "rp": "TeamsManager.UI_IntToVisibilityConverter.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 28, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.InverseBooleanConverter", "rp": "TeamsManager.UI_InverseBooleanConverter.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 28, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.InverseBooleanToVisibilityConverter", "rp": "TeamsManager.UI_InverseBooleanToVisibilityConverter.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 24, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.InverseBoolToVisibilityConverter", "rp": "TeamsManager.UI_InverseBoolToVisibilityConverter.html", "cl": 0, "ucl": 12, "cal": 12, "tl": 34, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.NullToVisibilityConverter", "rp": "TeamsManager.UI_NullToVisibilityConverter.html", "cl": 0, "ucl": 11, "cal": 11, "tl": 32, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.OperationStatusToColorConverter", "rp": "TeamsManager.UI_OperationStatusToColorConverter.html", "cl": 0, "ucl": 35, "cal": 35, "tl": 99, "cb": 0, "tb": 68, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.OperationStatusToTextColorConverter", "rp": "TeamsManager.UI_OperationStatusToTextColorConverter.html", "cl": 0, "ucl": 26, "cal": 26, "tl": 99, "cb": 0, "tb": 68, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.OperationTypeToIconConverter", "rp": "TeamsManager.UI_OperationTypeToIconConverter.html", "cl": 0, "ucl": 131, "cal": 131, "tl": 150, "cb": 0, "tb": 696, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.PercentageToColorConverter", "rp": "TeamsManager.UI_PercentageToColorConverter.html", "cl": 0, "ucl": 13, "cal": 13, "tl": 142, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.ProgressToPercentageConverter", "rp": "TeamsManager.UI_ProgressToPercentageConverter.html", "cl": 0, "ucl": 11, "cal": 11, "tl": 181, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.ProgressToTextConverter", "rp": "TeamsManager.UI_ProgressToTextConverter.html", "cl": 0, "ucl": 12, "cal": 12, "tl": 181, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.StepStatusConverter", "rp": "TeamsManager.UI_StepStatusConverter.html", "cl": 0, "ucl": 14, "cal": 14, "tl": 30, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.StringToBoolConverter", "rp": "TeamsManager.UI_StringToBoolConverter.html", "cl": 0, "ucl": 24, "cal": 24, "tl": 45, "cb": 0, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.StringToBrushConverter", "rp": "TeamsManager.UI_StringToBrushConverter.html", "cl": 0, "ucl": 12, "cal": 12, "tl": 35, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.StringToDateConverter", "rp": "TeamsManager.UI_StringToDateConverter.html", "cl": 0, "ucl": 29, "cal": 29, "tl": 54, "cb": 0, "tb": 14, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.StringToTimeConverter", "rp": "TeamsManager.UI_StringToTimeConverter.html", "cl": 0, "ucl": 35, "cal": 35, "tl": 66, "cb": 0, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.StringToVisibilityConverter", "rp": "TeamsManager.UI_StringToVisibilityConverter.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 24, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.TeamMemberRoleToStringConverter", "rp": "TeamsManager.UI_TeamMemberRoleToStringConverter.html", "cl": 0, "ucl": 17, "cal": 17, "tl": 37, "cb": 0, "tb": 14, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.TeamStatusToArchiveVisibilityConverter", "rp": "TeamsManager.UI_TeamStatusToArchiveVisibilityConverter.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 42, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.TeamStatusToColorConverter", "rp": "TeamsManager.UI_TeamStatusToColorConverter.html", "cl": 0, "ucl": 13, "cal": 13, "tl": 30, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.TeamStatusToRestoreVisibilityConverter", "rp": "TeamsManager.UI_TeamStatusToRestoreVisibilityConverter.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 42, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.TimeSpanToReadableConverter", "rp": "TeamsManager.UI_TimeSpanToReadableConverter.html", "cl": 0, "ucl": 45, "cal": 45, "tl": 181, "cb": 0, "tb": 26, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.TimeSpanToStringConverter", "rp": "TeamsManager.UI_TimeSpanToStringConverter.html", "cl": 0, "ucl": 14, "cal": 14, "tl": 142, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.UserRoleToPolishConverter", "rp": "TeamsManager.UI_UserRoleToPolishConverter.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 29, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.WorkloadToColorConverter", "rp": "TeamsManager.UI_WorkloadToColorConverter.html", "cl": 0, "ucl": 13, "cal": 13, "tl": 60, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Converters.WorkloadToColorSingleConverter", "rp": "TeamsManager.UI_WorkloadToColorSingleConverter.html", "cl": 0, "ucl": 13, "cal": 13, "tl": 60, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Examples.TeamTemplateEditorUsage", "rp": "TeamsManager.UI_TeamTemplateEditorUsage.html", "cl": 0, "ucl": 40, "cal": 40, "tl": 102, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.ConditionalAccessInfo", "rp": "TeamsManager.UI_ConditionalAccessInfo.html", "cl": 0, "ucl": 30, "cal": 30, "tl": 109, "cb": 0, "tb": 14, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Configuration.ApiConfiguration", "rp": "TeamsManager.UI_ApiConfiguration.html", "cl": 0, "ucl": 16, "cal": 16, "tl": 70, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Configuration.AzureAdConfiguration", "rp": "TeamsManager.UI_AzureAdConfiguration.html", "cl": 0, "ucl": 14, "cal": 14, "tl": 84, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Configuration.AzureAdSettings", "rp": "TeamsManager.UI_AzureAdSettings.html", "cl": 0, "ucl": 7, "cal": 7, "tl": 98, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Configuration.ConfigurationValidationResult", "rp": "TeamsManager.UI_ConfigurationValidationResult.html", "cl": 0, "ucl": 4, "cal": 4, "tl": 66, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Configuration.LoginSettings", "rp": "TeamsManager.UI_LoginSettings.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 35, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Configuration.MsalConfiguration", "rp": "TeamsManager.UI_MsalConfiguration.html", "cl": 0, "ucl": 26, "cal": 26, "tl": 98, "cb": 0, "tb": 14, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Configuration.OAuthConfiguration", "rp": "TeamsManager.UI_OAuthConfiguration.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 84, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.DialogOptions", "rp": "TeamsManager.UI_DialogOptions.html", "cl": 0, "ucl": 11, "cal": 11, "tl": 151, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.DialogResponse", "rp": "TeamsManager.UI_DialogResponse.html", "cl": 0, "ucl": 6, "cal": 6, "tl": 151, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Import.ColumnMappingModel", "rp": "TeamsManager.UI_ColumnMappingModel.html", "cl": 0, "ucl": 25, "cal": 25, "tl": 104, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Import.ImportDataTypeModel", "rp": "TeamsManager.UI_ImportDataTypeModel.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 104, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Import.PreviewRowModel", "rp": "TeamsManager.UI_PreviewRowModel.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 104, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Import.ValidationItemModel", "rp": "TeamsManager.UI_ValidationItemModel.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 104, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Monitoring.ActiveOperationData", "rp": "TeamsManager.UI_ActiveOperationData.html", "cl": 8, "ucl": 1, "cal": 9, "tl": 88, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Monitoring.HealthComponent", "rp": "TeamsManager.UI_HealthComponent.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 88, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Monitoring.MonitoringDashboardSummary", "rp": "TeamsManager.UI_MonitoringDashboardSummary.html", "cl": 5, "ucl": 0, "cal": 5, "tl": 88, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Monitoring.SystemAlert", "rp": "TeamsManager.UI_SystemAlert.html", "cl": 7, "ucl": 0, "cal": 7, "tl": 88, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Monitoring.SystemHealthData", "rp": "TeamsManager.UI_SystemHealthData.html", "cl": 3, "ucl": 0, "cal": 3, "tl": 88, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Monitoring.SystemMetrics", "rp": "TeamsManager.UI_SystemMetrics.html", "cl": 10, "ucl": 0, "cal": 10, "tl": 88, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.SchoolTypeModels.SchoolTypeDisplayModel", "rp": "TeamsManager.UI_SchoolTypeDisplayModel.html", "cl": 0, "ucl": 37, "cal": 37, "tl": 77, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "rp": "TeamsManager.UI_SchoolYearDisplayModel.html", "cl": 0, "ucl": 168, "cal": 168, "tl": 323, "cb": 0, "tb": 102, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Teams.TeamGrouping", "rp": "TeamsManager.UI_TeamGrouping.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 14, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.Teams.TemplateValueViewModel", "rp": "TeamsManager.UI_TemplateValueViewModel.html", "cl": 0, "ucl": 21, "cal": 21, "tl": 47, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.TestCase", "rp": "TeamsManager.UI_TestCase.html", "cl": 0, "ucl": 15, "cal": 15, "tl": 52, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.TestSuite", "rp": "TeamsManager.UI_TestSuite.html", "cl": 0, "ucl": 11, "cal": 11, "tl": 52, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.UI.DepartmentStatistics", "rp": "TeamsManager.UI_DepartmentStatistics.html", "cl": 0, "ucl": 6, "cal": 6, "tl": 15, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.ViewModels.SchoolTypeAssignmentModel", "rp": "TeamsManager.UI_SchoolTypeAssignmentModel.html", "cl": 0, "ucl": 82, "cal": 82, "tl": 213, "cb": 0, "tb": 16, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "rp": "TeamsManager.UI_UserDetailModel.html", "cl": 0, "ucl": 240, "cal": 240, "tl": 423, "cb": 0, "tb": 152, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Scripts.CreateDefaultOrganizationalUnit", "rp": "TeamsManager.UI_CreateDefaultOrganizationalUnit.html", "cl": 0, "ucl": 68, "cal": 68, "tl": 109, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Scripts.TestDepartmentCRUD", "rp": "TeamsManager.UI_TestDepartmentCRUD.html", "cl": 0, "ucl": 207, "cal": 207, "tl": 324, "cb": 0, "tb": 46, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.ApplicationSettingService", "rp": "TeamsManager.UI_ApplicationSettingService.html", "cl": 0, "ucl": 153, "cal": 153, "tl": 282, "cb": 0, "tb": 38, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.AzureAdUiConfig", "rp": "TeamsManager.UI_AzureAdUiConfig.html", "cl": 7, "ucl": 0, "cal": 7, "tl": 314, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.ConditionalAccessAnalyzer", "rp": "TeamsManager.UI_ConditionalAccessAnalyzer.html", "cl": 0, "ucl": 158, "cal": 158, "tl": 266, "cb": 0, "tb": 72, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.Configuration.ConfigurationManager", "rp": "TeamsManager.UI_ConfigurationManager.html", "cl": 0, "ucl": 170, "cal": 170, "tl": 330, "cb": 0, "tb": 44, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.Configuration.ConfigurationValidator", "rp": "TeamsManager.UI_ConfigurationValidator.html", "cl": 0, "ucl": 212, "cal": 212, "tl": 322, "cb": 0, "tb": 82, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.Configuration.EncryptionService", "rp": "TeamsManager.UI_EncryptionService.html", "cl": 0, "ucl": 49, "cal": 49, "tl": 113, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.Configuration.MsalConfigurationProvider", "rp": "TeamsManager.UI_MsalConfigurationProvider.html", "cl": 0, "ucl": 88, "cal": 88, "tl": 165, "cb": 0, "tb": 22, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.Dashboard.SimpleDashboardOperationHistoryService", "rp": "TeamsManager.UI_SimpleDashboardOperationHistoryService.html", "cl": 0, "ucl": 181, "cal": 181, "tl": 256, "cb": 0, "tb": 42, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.Dashboard.SimpleDashboardTeamService", "rp": "TeamsManager.UI_SimpleDashboardTeamService.html", "cl": 0, "ucl": 95, "cal": 95, "tl": 155, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.Dashboard.SimpleDashboardUserService", "rp": "TeamsManager.UI_SimpleDashboardUserService.html", "cl": 0, "ucl": 79, "cal": 79, "tl": 127, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.DepartmentCodeChange", "rp": "TeamsManager.UI_DepartmentCodeChange.html", "cl": 0, "ucl": 4, "cal": 4, "tl": 277, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.DepartmentCodeMigrationService", "rp": "TeamsManager.UI_DepartmentCodeMigrationService.html", "cl": 0, "ucl": 131, "cal": 131, "tl": 277, "cb": 0, "tb": 36, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.DialogHelpers", "rp": "TeamsManager.UI_DialogHelpers.html", "cl": 0, "ucl": 97, "cal": 97, "tl": 217, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.GraphTestResult", "rp": "TeamsManager.UI_GraphTestResult.html", "cl": 0, "ucl": 4, "cal": 4, "tl": 245, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.GraphUserProfileService", "rp": "TeamsManager.UI_GraphUserProfileService.html", "cl": 0, "ucl": 140, "cal": 140, "tl": 245, "cb": 0, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.Http.TokenAuthorizationHandler", "rp": "TeamsManager.UI_TokenAuthorizationHandler.html", "cl": 0, "ucl": 28, "cal": 28, "tl": 70, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.ManualTestingService", "rp": "TeamsManager.UI_ManualTestingService.html", "cl": 0, "ucl": 361, "cal": 361, "tl": 424, "cb": 0, "tb": 21, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.MigrationResult", "rp": "TeamsManager.UI_MigrationResult.html", "cl": 0, "ucl": 34, "cal": 34, "tl": 277, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.MonitoringDataService", "rp": "TeamsManager.UI_MonitoringDataService.html", "cl": 179, "ucl": 226, "cal": 405, "tl": 536, "cb": 36, "tb": 164, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.MonitoringPerformanceOptimizer", "rp": "TeamsManager.UI_MonitoringPerformanceOptimizer.html", "cl": 0, "ucl": 234, "cal": 234, "tl": 473, "cb": 0, "tb": 42, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.MsalAuthService", "rp": "TeamsManager.UI_MsalAuthService.html", "cl": 28, "ucl": 200, "cal": 228, "tl": 314, "cb": 8, "tb": 78, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.MsalCacheHelper", "rp": "TeamsManager.UI_MsalCacheHelper.html", "cl": 0, "ucl": 87, "cal": 87, "tl": 153, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.MsalUiAppConfiguration", "rp": "TeamsManager.UI_MsalUiAppConfiguration.html", "cl": 2, "ucl": 0, "cal": 2, "tl": 314, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.OptimizationStatistics", "rp": "TeamsManager.UI_OptimizationStatistics.html", "cl": 0, "ucl": 16, "cal": 16, "tl": 473, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.SignalRService", "rp": "TeamsManager.UI_SignalRService.html", "cl": 0, "ucl": 262, "cal": 262, "tl": 400, "cb": 0, "tb": 38, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.SimpleUserService", "rp": "TeamsManager.UI_SimpleUserService.html", "cl": 0, "ucl": 219, "cal": 219, "tl": 348, "cb": 0, "tb": 32, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.SystemHealthDataComparer", "rp": "TeamsManager.UI_SystemHealthDataComparer.html", "cl": 0, "ucl": 25, "cal": 25, "tl": 473, "cb": 0, "tb": 20, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.TeamsManagerAlert", "rp": "TeamsManager.UI_TeamsManagerAlert.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 727, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.TeamsManagerApiService", "rp": "TeamsManager.UI_TeamsManagerApiService.html", "cl": 0, "ucl": 393, "cal": 393, "tl": 651, "cb": 0, "tb": 52, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.TeamsManagerDashboardSummary", "rp": "TeamsManager.UI_TeamsManagerDashboardSummary.html", "cl": 0, "ucl": 16, "cal": 16, "tl": 727, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.TeamsManagerHealthComponent", "rp": "TeamsManager.UI_TeamsManagerHealthComponent.html", "cl": 0, "ucl": 6, "cal": 6, "tl": 727, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.TeamsManagerHealthData", "rp": "TeamsManager.UI_TeamsManagerHealthData.html", "cl": 0, "ucl": 3, "cal": 3, "tl": 727, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.TeamsManagerMetrics", "rp": "TeamsManager.UI_TeamsManagerMetrics.html", "cl": 0, "ucl": 18, "cal": 18, "tl": 727, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.TeamsManagerMonitoringService", "rp": "TeamsManager.UI_TeamsManagerMonitoringService.html", "cl": 0, "ucl": 461, "cal": 461, "tl": 727, "cb": 0, "tb": 92, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.TeamsManagerOperation", "rp": "TeamsManager.UI_TeamsManagerOperation.html", "cl": 0, "ucl": 9, "cal": 9, "tl": 727, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.UI.DepartmentTreeService", "rp": "TeamsManager.UI_DepartmentTreeService.html", "cl": 0, "ucl": 74, "cal": 74, "tl": 128, "cb": 0, "tb": 32, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.UI.SchoolTypeUIService", "rp": "TeamsManager.UI_SchoolTypeUIService.html", "cl": 0, "ucl": 122, "cal": 122, "tl": 203, "cb": 0, "tb": 42, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.UI.SchoolYearUIService", "rp": "TeamsManager.UI_SchoolYearUIService.html", "cl": 0, "ucl": 228, "cal": 228, "tl": 383, "cb": 0, "tb": 56, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.UIDialogService", "rp": "TeamsManager.UI_UIDialogService.html", "cl": 0, "ucl": 187, "cal": 187, "tl": 266, "cb": 0, "tb": 24, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.UserProfile", "rp": "TeamsManager.UI_UserProfile.html", "cl": 0, "ucl": 18, "cal": 18, "tl": 245, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Services.UserSynchronizationService", "rp": "TeamsManager.UI_UserSynchronizationService.html", "cl": 0, "ucl": 121, "cal": 121, "tl": 208, "cb": 0, "tb": 66, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.BulkOperationsToolbar", "rp": "TeamsManager.UI_BulkOperationsToolbar.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 223, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.ChannelCard", "rp": "TeamsManager.UI_ChannelCard.html", "cl": 0, "ucl": 9, "cal": 9, "tl": 283, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.Import.ColumnMappingStep", "rp": "TeamsManager.UI_ColumnMappingStep.html", "cl": 0, "ucl": 30, "cal": 30, "tl": 266, "cb": 0, "tb": 16, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.Import.FileSelectionStep", "rp": "TeamsManager.UI_FileSelectionStep.html", "cl": 0, "ucl": 82, "cal": 82, "tl": 348, "cb": 0, "tb": 24, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.Import.ProgressStep", "rp": "TeamsManager.UI_ProgressStep.html", "cl": 0, "ucl": 1, "cal": 1, "tl": 338, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.Import.ValidationStep", "rp": "TeamsManager.UI_ValidationStep.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 392, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.Settings.SettingEditorControl", "rp": "TeamsManager.UI_SettingEditorControl.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 173, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.Teams.TemplatePreviewControl", "rp": "TeamsManager.UI_TemplatePreviewControl.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 272, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.Teams.TestDataDialog", "rp": "TeamsManager.UI_TestDataDialog.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 209, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.UserControls.Teams.TokenHelperPanel", "rp": "TeamsManager.UI_TokenHelperPanel.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 334, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.AsyncRelayCommand", "rp": "TeamsManager.UI_AsyncRelayCommand.html", "cl": 0, "ucl": 31, "cal": 31, "tl": 228, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.AsyncRelayCommand<T>", "rp": "TeamsManager.UI_AsyncRelayCommand_1.html", "cl": 0, "ucl": 28, "cal": 28, "tl": 228, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.BaseViewModel", "rp": "TeamsManager.UI_BaseViewModel.html", "cl": 0, "ucl": 44, "cal": 44, "tl": 111, "cb": 0, "tb": 18, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Dashboard.DashboardViewModel", "rp": "TeamsManager.UI_DashboardViewModel.html", "cl": 0, "ucl": 224, "cal": 224, "tl": 450, "cb": 0, "tb": 36, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Dashboard.NotificationItem", "rp": "TeamsManager.UI_NotificationItem.html", "cl": 0, "ucl": 3, "cal": 3, "tl": 450, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Dashboard.OperationHistoryItem", "rp": "TeamsManager.UI_OperationHistoryItem.html", "cl": 0, "ucl": 8, "cal": 8, "tl": 450, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "rp": "TeamsManager.UI_DepartmentEditViewModel.html", "cl": 0, "ucl": 593, "cal": 593, "tl": 1089, "cb": 0, "tb": 222, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "rp": "TeamsManager.UI_DepartmentsManagementViewModel.html", "cl": 0, "ucl": 495, "cal": 495, "tl": 782, "cb": 0, "tb": 170, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Departments.DepartmentTreeItemViewModel", "rp": "TeamsManager.UI_DepartmentTreeItemViewModel.html", "cl": 0, "ucl": 48, "cal": 48, "tl": 89, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "rp": "TeamsManager.UI_UniversalDialogViewModel.html", "cl": 0, "ucl": 170, "cal": 170, "tl": 327, "cb": 0, "tb": 118, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Import.BulkImportWizardViewModel", "rp": "TeamsManager.UI_BulkImportWizardViewModel.html", "cl": 0, "ucl": 250, "cal": 250, "tl": 379, "cb": 0, "tb": 71, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Import.ImportColumnMappingViewModel", "rp": "TeamsManager.UI_ImportColumnMappingViewModel.html", "cl": 0, "ucl": 304, "cal": 304, "tl": 429, "cb": 0, "tb": 142, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Import.ImportFileSelectionViewModel", "rp": "TeamsManager.UI_ImportFileSelectionViewModel.html", "cl": 0, "ucl": 200, "cal": 200, "tl": 343, "cb": 0, "tb": 48, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Import.ImportProgressViewModel", "rp": "TeamsManager.UI_ImportProgressViewModel.html", "cl": 0, "ucl": 325, "cal": 325, "tl": 509, "cb": 0, "tb": 94, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Import.ImportValidationViewModel", "rp": "TeamsManager.UI_ImportValidationViewModel.html", "cl": 0, "ucl": 281, "cal": 281, "tl": 464, "cb": 0, "tb": 90, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.LoginViewModel", "rp": "TeamsManager.UI_LoginViewModel.html", "cl": 0, "ucl": 156, "cal": 156, "tl": 294, "cb": 0, "tb": 44, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Monitoring.MonitoringDashboardViewModel", "rp": "TeamsManager.UI_MonitoringDashboardViewModel.html", "cl": 0, "ucl": 221, "cal": 221, "tl": 337, "cb": 0, "tb": 42, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Monitoring.Widgets.ActiveOperationsWidgetViewModel", "rp": "TeamsManager.UI_ActiveOperationsWidgetViewModel.html", "cl": 0, "ucl": 79, "cal": 79, "tl": 150, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Monitoring.Widgets.ActiveOperationViewModel", "rp": "TeamsManager.UI_ActiveOperationViewModel.html", "cl": 0, "ucl": 9, "cal": 9, "tl": 150, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Monitoring.Widgets.AdvancedPerformanceChartWidgetViewModel", "rp": "TeamsManager.UI_AdvancedPerformanceChartWidgetViewModel.html", "cl": 0, "ucl": 318, "cal": 318, "tl": 501, "cb": 0, "tb": 39, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Monitoring.Widgets.AlertsWidgetViewModel", "rp": "TeamsManager.UI_AlertsWidgetViewModel.html", "cl": 0, "ucl": 121, "cal": 121, "tl": 207, "cb": 0, "tb": 14, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Monitoring.Widgets.HealthComponent", "rp": "TeamsManager.UI_HealthComponent.2.html", "cl": 0, "ucl": 46, "cal": 46, "tl": 560, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Monitoring.Widgets.SystemAlertViewModel", "rp": "TeamsManager.UI_SystemAlertViewModel.html", "cl": 0, "ucl": 7, "cal": 7, "tl": 207, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerHealthWidgetViewModel", "rp": "TeamsManager.UI_TeamsManagerHealthWidgetViewModel.html", "cl": 0, "ucl": 336, "cal": 336, "tl": 560, "cb": 0, "tb": 104, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerMetricsWidgetViewModel", "rp": "TeamsManager.UI_TeamsManagerMetricsWidgetViewModel.html", "cl": 0, "ucl": 199, "cal": 199, "tl": 383, "cb": 0, "tb": 86, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Operations.OperationHistoryItemViewModel", "rp": "TeamsManager.UI_OperationHistoryItemViewModel.html", "cl": 0, "ucl": 122, "cal": 122, "tl": 292, "cb": 0, "tb": 58, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Operations.OperationHistoryViewModel", "rp": "TeamsManager.UI_OperationHistoryViewModel.html", "cl": 0, "ucl": 561, "cal": 561, "tl": 960, "cb": 0, "tb": 138, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitDisplayItem", "rp": "TeamsManager.UI_OrganizationalUnitDisplayItem.html", "cl": 0, "ucl": 13, "cal": 13, "tl": 1226, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "rp": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "cl": 0, "ucl": 638, "cal": 638, "tl": 1226, "cb": 0, "tb": 246, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel", "rp": "TeamsManager.UI_OrganizationalUnitsManagementViewModel.html", "cl": 0, "ucl": 321, "cal": 321, "tl": 524, "cb": 0, "tb": 100, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitTreeItemViewModel", "rp": "TeamsManager.UI_OrganizationalUnitTreeItemViewModel.html", "cl": 0, "ucl": 135, "cal": 135, "tl": 245, "cb": 0, "tb": 56, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.RelayCommand", "rp": "TeamsManager.UI_RelayCommand.html", "cl": 0, "ucl": 19, "cal": 19, "tl": 228, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.RelayCommand<T>", "rp": "TeamsManager.UI_RelayCommand_1.html", "cl": 0, "ucl": 18, "cal": 18, "tl": 228, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypeEditViewModel", "rp": "TeamsManager.UI_SchoolTypeEditViewModel.html", "cl": 0, "ucl": 156, "cal": 156, "tl": 278, "cb": 0, "tb": 52, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypesListViewModel", "rp": "TeamsManager.UI_SchoolTypesListViewModel.html", "cl": 0, "ucl": 185, "cal": 185, "tl": 322, "cb": 0, "tb": 52, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.SchoolYears.SchoolYearListViewModel", "rp": "TeamsManager.UI_SchoolYearListViewModel.html", "cl": 0, "ucl": 220, "cal": 220, "tl": 415, "cb": 0, "tb": 52, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingItemViewModel", "rp": "TeamsManager.UI_ApplicationSettingItemViewModel.html", "cl": 0, "ucl": 239, "cal": 239, "tl": 388, "cb": 0, "tb": 102, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingsViewModel", "rp": "TeamsManager.UI_ApplicationSettingsViewModel.html", "cl": 0, "ucl": 207, "cal": 207, "tl": 362, "cb": 0, "tb": 60, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Shell.MainShellViewModel", "rp": "TeamsManager.UI_MainShellViewModel.html", "cl": 0, "ucl": 645, "cal": 645, "tl": 985, "cb": 0, "tb": 120, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Subjects.ColumnMapping", "rp": "TeamsManager.UI_ColumnMapping.html", "cl": 0, "ucl": 25, "cal": 25, "tl": 626, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Subjects.SubjectEditViewModel", "rp": "TeamsManager.UI_SubjectEditViewModel.html", "cl": 0, "ucl": 177, "cal": 177, "tl": 318, "cb": 0, "tb": 54, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Subjects.SubjectImportViewModel", "rp": "TeamsManager.UI_SubjectImportViewModel.html", "cl": 0, "ucl": 292, "cal": 292, "tl": 626, "cb": 0, "tb": 76, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Subjects.SubjectPreview", "rp": "TeamsManager.UI_SubjectPreview.html", "cl": 0, "ucl": 41, "cal": 41, "tl": 626, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Subjects.SubjectsViewModel", "rp": "TeamsManager.UI_SubjectsViewModel.html", "cl": 0, "ucl": 339, "cal": 339, "tl": 506, "cb": 0, "tb": 84, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.ChannelCardViewModel", "rp": "TeamsManager.UI_ChannelCardViewModel.html", "cl": 0, "ucl": 257, "cal": 257, "tl": 455, "cb": 0, "tb": 94, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.ChannelDeletedEventArgs", "rp": "TeamsManager.UI_ChannelDeletedEventArgs.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 455, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.ChannelUpdatedEventArgs", "rp": "TeamsManager.UI_ChannelUpdatedEventArgs.html", "cl": 0, "ucl": 5, "cal": 5, "tl": 455, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.PlaceholderToken", "rp": "TeamsManager.UI_PlaceholderToken.html", "cl": 0, "ucl": 35, "cal": 35, "tl": 112, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.TeamChannelsViewModel", "rp": "TeamsManager.UI_TeamChannelsViewModel.html", "cl": 0, "ucl": 328, "cal": 328, "tl": 560, "cb": 0, "tb": 108, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "rp": "TeamsManager.UI_TeamCreationWizardViewModel.html", "cl": 0, "ucl": 369, "cal": 369, "tl": 588, "cb": 0, "tb": 189, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.TeamLifecycleDialogViewModel", "rp": "TeamsManager.UI_TeamLifecycleDialogViewModel.html", "cl": 0, "ucl": 382, "cal": 382, "tl": 671, "cb": 0, "tb": 66, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.TeamListViewModel", "rp": "TeamsManager.UI_TeamListViewModel.html", "cl": 0, "ucl": 373, "cal": 373, "tl": 567, "cb": 0, "tb": 102, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.TeamMembersViewModel", "rp": "TeamsManager.UI_TeamMembersViewModel.html", "cl": 0, "ucl": 274, "cal": 274, "tl": 437, "cb": 0, "tb": 58, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Teams.TeamTemplateEditorViewModel", "rp": "TeamsManager.UI_TeamTemplateEditorViewModel.html", "cl": 0, "ucl": 358, "cal": 358, "tl": 655, "cb": 0, "tb": 66, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Users.UserDetailViewModel", "rp": "TeamsManager.UI_UserDetailViewModel.html", "cl": 0, "ucl": 312, "cal": 312, "tl": 516, "cb": 0, "tb": 58, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Users.UserListItemViewModel", "rp": "TeamsManager.UI_UserListItemViewModel.html", "cl": 0, "ucl": 61, "cal": 61, "tl": 114, "cb": 0, "tb": 34, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "rp": "TeamsManager.UI_UserListViewModel.html", "cl": 0, "ucl": 491, "cal": 491, "tl": 799, "cb": 0, "tb": 132, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel", "rp": "TeamsManager.UI_UserSchoolTypeAssignmentViewModel.html", "cl": 0, "ucl": 297, "cal": 297, "tl": 514, "cb": 0, "tb": 100, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Common.ConfirmationDialog", "rp": "TeamsManager.UI_ConfirmationDialog.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 180, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Dashboard.DashboardView", "rp": "TeamsManager.UI_DashboardView.html", "cl": 0, "ucl": 16, "cal": 16, "tl": 485, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Departments.DepartmentEditDialog", "rp": "TeamsManager.UI_DepartmentEditDialog.html", "cl": 0, "ucl": 28, "cal": 28, "tl": 473, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Departments.DepartmentsManagementView", "rp": "TeamsManager.UI_DepartmentsManagementView.html", "cl": 0, "ucl": 18, "cal": 18, "tl": 556, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Dialogs.UniversalDialog", "rp": "TeamsManager.UI_UniversalDialog.html", "cl": 0, "ucl": 56, "cal": 56, "tl": 423, "cb": 0, "tb": 24, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Import.BulkImportWizardWindow", "rp": "TeamsManager.UI_BulkImportWizardWindow.html", "cl": 0, "ucl": 35, "cal": 35, "tl": 319, "cb": 0, "tb": 12, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.LoginWindow", "rp": "TeamsManager.UI_LoginWindow.html", "cl": 0, "ucl": 43, "cal": 43, "tl": 386, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.ManualTestingWindow", "rp": "TeamsManager.UI_ManualTestingWindow.html", "cl": 0, "ucl": 830, "cal": 830, "tl": 1757, "cb": 0, "tb": 195, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Monitoring.MonitoringDashboardView", "rp": "TeamsManager.UI_MonitoringDashboardView.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 302, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Monitoring.Widgets.ActiveOperationsWidget", "rp": "TeamsManager.UI_ActiveOperationsWidget.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 198, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Monitoring.Widgets.AdvancedPerformanceChartWidget", "rp": "TeamsManager.UI_AdvancedPerformanceChartWidget.html", "cl": 0, "ucl": 21, "cal": 21, "tl": 213, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Monitoring.Widgets.AlertsWidget", "rp": "TeamsManager.UI_AlertsWidget.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 234, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Monitoring.Widgets.SystemHealthPieChartWidget", "rp": "TeamsManager.UI_SystemHealthPieChartWidget.html", "cl": 0, "ucl": 7, "cal": 7, "tl": 367, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Monitoring.Widgets.TeamsManagerHealthWidget", "rp": "TeamsManager.UI_TeamsManagerHealthWidget.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 432, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Monitoring.Widgets.TeamsManagerMetricsWidget", "rp": "TeamsManager.UI_TeamsManagerMetricsWidget.html", "cl": 0, "ucl": 11, "cal": 11, "tl": 365, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Operations.OperationHistoryView", "rp": "TeamsManager.UI_OperationHistoryView.html", "cl": 0, "ucl": 24, "cal": 24, "tl": 429, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.OrganizationalUnits.OrganizationalUnitEditDialog", "rp": "TeamsManager.UI_OrganizationalUnitEditDialog.html", "cl": 0, "ucl": 29, "cal": 29, "tl": 469, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.OrganizationalUnits.OrganizationalUnitsManagementView", "rp": "TeamsManager.UI_OrganizationalUnitsManagementView.html", "cl": 0, "ucl": 19, "cal": 19, "tl": 537, "cb": 0, "tb": 10, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.SchoolTypes.SchoolTypeEditDialog", "rp": "TeamsManager.UI_SchoolTypeEditDialog.html", "cl": 0, "ucl": 36, "cal": 36, "tl": 463, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.SchoolTypes.SchoolTypesListView", "rp": "TeamsManager.UI_SchoolTypesListView.html", "cl": 0, "ucl": 20, "cal": 20, "tl": 321, "cb": 0, "tb": 16, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.SchoolYears.SchoolYearListView", "rp": "TeamsManager.UI_SchoolYearListView.html", "cl": 0, "ucl": 9, "cal": 9, "tl": 451, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Settings.ApplicationSettingsView", "rp": "TeamsManager.UI_ApplicationSettingsView.html", "cl": 0, "ucl": 19, "cal": 19, "tl": 515, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Shell.MainShellWindow", "rp": "TeamsManager.UI_MainShellWindow.html", "cl": 0, "ucl": 72, "cal": 72, "tl": 765, "cb": 0, "tb": 14, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Subjects.SubjectEditDialog", "rp": "TeamsManager.UI_SubjectEditDialog.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 286, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Subjects.SubjectImportDialog", "rp": "TeamsManager.UI_SubjectImportDialog.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 402, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Subjects.SubjectsView", "rp": "TeamsManager.UI_SubjectsView.html", "cl": 0, "ucl": 13, "cal": 13, "tl": 287, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Subjects.SubjectTeachersDialog", "rp": "TeamsManager.UI_SubjectTeachersDialog.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 256, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Teams.TeamChannelsView", "rp": "TeamsManager.UI_TeamChannelsView.html", "cl": 0, "ucl": 14, "cal": 14, "tl": 432, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Teams.TeamCreationWizardWindow", "rp": "TeamsManager.UI_TeamCreationWizardWindow.html", "cl": 0, "ucl": 21, "cal": 21, "tl": 696, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Teams.TeamLifecycleDialog", "rp": "TeamsManager.UI_TeamLifecycleDialog.html", "cl": 0, "ucl": 10, "cal": 10, "tl": 606, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Teams.TeamListView", "rp": "TeamsManager.UI_TeamListView.html", "cl": 0, "ucl": 17, "cal": 17, "tl": 361, "cb": 0, "tb": 4, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Teams.TeamMembersView", "rp": "TeamsManager.UI_TeamMembersView.html", "cl": 0, "ucl": 9, "cal": 9, "tl": 282, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Teams.TeamTemplateEditorWindow", "rp": "TeamsManager.UI_TeamTemplateEditorWindow.html", "cl": 0, "ucl": 28, "cal": 28, "tl": 568, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.TestCaseViewModel", "rp": "TeamsManager.UI_TestCaseViewModel.html", "cl": 0, "ucl": 6, "cal": 6, "tl": 1183, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.TestStepViewModel", "rp": "TeamsManager.UI_TestStepViewModel.html", "cl": 0, "ucl": 2, "cal": 2, "tl": 1183, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.UserProfileWindow", "rp": "TeamsManager.UI_UserProfileWindow.html", "cl": 0, "ucl": 23, "cal": 23, "tl": 486, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Users.UserDetailWindow", "rp": "TeamsManager.UI_UserDetailWindow.html", "cl": 0, "ucl": 33, "cal": 33, "tl": 646, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Users.UserListView", "rp": "TeamsManager.UI_UserListView.html", "cl": 0, "ucl": 20, "cal": 20, "tl": 630, "cb": 0, "tb": 6, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "TeamsManager.UI.Views.Users.UserSchoolTypeAssignmentView", "rp": "TeamsManager.UI_UserSchoolTypeAssignmentView.html", "cl": 0, "ucl": 6, "cal": 6, "tl": 448, "cb": 0, "tb": 2, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "VisualTreeExtensions", "rp": "TeamsManager.UI_VisualTreeExtensions.html", "cl": 0, "ucl": 17, "cal": 17, "tl": 1183, "cb": 0, "tb": 8, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
      { "name": "XamlGeneratedNamespace.GeneratedInternalTypeHelper", "rp": "TeamsManager.UI_GeneratedInternalTypeHelper.html", "cl": 0, "ucl": 19, "cal": 19, "tl": 2, "cb": 0, "tb": 0, "cm": 0, "fcm": 0, "tm": 0, "lch": [], "bch": [], "mch": [], "mfch": [], "hc": [], "metrics": { } },
    ]},
];

var metrics = [{ "name": "Crap Score", "abbreviation": "crp", "explanationUrl": "https://googletesting.blogspot.de/2011/02/this-code-is-crap.html" }, { "name": "Cyclomatic complexity", "abbreviation": "cc", "explanationUrl": "https://en.wikipedia.org/wiki/Cyclomatic_complexity" }, { "name": "Line coverage", "abbreviation": "cov", "explanationUrl": "https://en.wikipedia.org/wiki/Code_coverage" }, { "name": "Branch coverage", "abbreviation": "bcov", "explanationUrl": "https://en.wikipedia.org/wiki/Code_coverage" }];

var historicCoverageExecutionTimes = [];

var riskHotspotMetrics = [
      { "name": "Crap Score", "explanationUrl": "https://googletesting.blogspot.de/2011/02/this-code-is-crap.html" },
      { "name": "Cyclomatic complexity", "explanationUrl": "https://en.wikipedia.org/wiki/Cyclomatic_complexity" },
];

var riskHotspots = [
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.OperationTypeToIconConverter", "reportPath": "TeamsManager.UI_OperationTypeToIconConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 485112, "exceeded": true },
      { "value": 696, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapToGraphTeam(System.Object)", "methodShortName": "MapToGraphTeam(...)", "fileIndex": 0, "line": 1596,
    "metrics": [
      { "value": 19740, "exceeded": true },
      { "value": 140, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapToGraphTeam(System.Object)", "methodShortName": "MapToGraphTeam(...)", "fileIndex": 0, "line": 1621,
    "metrics": [
      { "value": 19740, "exceeded": true },
      { "value": 140, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapToGraphTeamMember(System.Object)", "methodShortName": "MapToGraphTeamMember(...)", "fileIndex": 0, "line": 1797,
    "metrics": [
      { "value": 18632, "exceeded": true },
      { "value": 136, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapToGraphTeamMember(System.Object)", "methodShortName": "MapToGraphTeamMember(...)", "fileIndex": 0, "line": 1822,
    "metrics": [
      { "value": 18632, "exceeded": true },
      { "value": 136, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphConnectionService", "reportPath": "TeamsManager.Core_GraphConnectionService.html", "methodName": "GetUserContextAsync()", "methodShortName": "GetUserContextAsync()", "fileIndex": 0, "line": 829,
    "metrics": [
      { "value": 7482, "exceeded": true },
      { "value": 86, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "SynchronizeTeamWithGraphAsync()", "methodShortName": "SynchronizeTeamWithGraphAsync()", "fileIndex": 0, "line": 1243,
    "metrics": [
      { "value": 7140, "exceeded": true },
      { "value": 84, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkRemoveUsersFromTeamV2Async()", "methodShortName": "BulkRemoveUsersFromTeamV2Async()", "fileIndex": 0, "line": 1378,
    "metrics": [
      { "value": 6480, "exceeded": true },
      { "value": 80, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkRemoveUsersFromTeamV2Async()", "methodShortName": "BulkRemoveUsersFromTeamV2Async()", "fileIndex": 0, "line": 1384,
    "metrics": [
      { "value": 6480, "exceeded": true },
      { "value": 80, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapToGraphChannel(System.Object,System.String)", "methodShortName": "MapToGraphChannel(...)", "fileIndex": 0, "line": 1843,
    "metrics": [
      { "value": 6162, "exceeded": true },
      { "value": 78, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapToGraphChannel(System.Object,System.String)", "methodShortName": "MapToGraphChannel(...)", "fileIndex": 0, "line": 1868,
    "metrics": [
      { "value": 6162, "exceeded": true },
      { "value": 78, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphConnectionService", "reportPath": "TeamsManager.Core_GraphConnectionService.html", "methodName": "ExecuteBatchRequestAsync()", "methodShortName": "ExecuteBatchRequestAsync()", "fileIndex": 0, "line": 972,
    "metrics": [
      { "value": 4970, "exceeded": true },
      { "value": 70, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.OperationStatusToColorConverter", "reportPath": "TeamsManager.UI_OperationStatusToColorConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 30,
    "metrics": [
      { "value": 4692, "exceeded": true },
      { "value": 68, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.OperationStatusToTextColorConverter", "reportPath": "TeamsManager.UI_OperationStatusToTextColorConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 80,
    "metrics": [
      { "value": 4692, "exceeded": true },
      { "value": 68, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "EnrichTeamWithGroupInfoAsync()", "methodShortName": "EnrichTeamWithGroupInfoAsync()", "fileIndex": 0, "line": 1637,
    "metrics": [
      { "value": 3660, "exceeded": true },
      { "value": 60, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamsByOwnerAsync()", "methodShortName": "GetTeamsByOwnerAsync()", "fileIndex": 0, "line": 520,
    "metrics": [
      { "value": 3660, "exceeded": true },
      { "value": 60, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "EnrichTeamWithGroupInfoAsync()", "methodShortName": "EnrichTeamWithGroupInfoAsync()", "fileIndex": 0, "line": 1662,
    "metrics": [
      { "value": 3660, "exceeded": true },
      { "value": 60, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamsByOwnerAsync()", "methodShortName": "GetTeamsByOwnerAsync()", "fileIndex": 0, "line": 545,
    "metrics": [
      { "value": 3660, "exceeded": true },
      { "value": 60, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "AddUsersToTeamAsync()", "methodShortName": "AddUsersToTeamAsync()", "fileIndex": 0, "line": 1568,
    "metrics": [
      { "value": 3660, "exceeded": true },
      { "value": 60, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkAddUsersToTeamV2Async()", "methodShortName": "BulkAddUsersToTeamV2Async()", "fileIndex": 0, "line": 1228,
    "metrics": [
      { "value": 3422, "exceeded": true },
      { "value": 58, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkRemoveUsersFromTeamAsync()", "methodShortName": "BulkRemoveUsersFromTeamAsync()", "fileIndex": 0, "line": 165,
    "metrics": [
      { "value": 3422, "exceeded": true },
      { "value": 58, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkAddUsersToTeamV2Async()", "methodShortName": "BulkAddUsersToTeamV2Async()", "fileIndex": 0, "line": 1234,
    "metrics": [
      { "value": 3422, "exceeded": true },
      { "value": 58, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Extensions.EnumExtensions", "reportPath": "TeamsManager.Core_EnumExtensions.html", "methodName": "ToPolishString(TeamsManager.Core.Enums.OperationType)", "methodShortName": "ToPolishString(...)", "fileIndex": 0, "line": 28,
    "metrics": [
      { "value": 3230, "exceeded": true },
      { "value": 68, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UserSynchronizationService", "reportPath": "TeamsManager.UI_UserSynchronizationService.html", "methodName": "SynchronizeLoggedUserAsync()", "methodShortName": "SynchronizeLoggedUserAsync()", "fileIndex": 0, "line": 47,
    "metrics": [
      { "value": 3192, "exceeded": true },
      { "value": 56, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetUserByUpnAsync()", "methodShortName": "GetUserByUpnAsync()", "fileIndex": 0, "line": 1495,
    "metrics": [
      { "value": 2970, "exceeded": true },
      { "value": 54, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetUserByUpnAsync()", "methodShortName": "GetUserByUpnAsync()", "fileIndex": 0, "line": 1520,
    "metrics": [
      { "value": 2970, "exceeded": true },
      { "value": 54, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.ChannelSynchronizer", "reportPath": "TeamsManager.Core_ChannelSynchronizer.html", "methodName": "MapProperties(TeamsManager.Core.Models.Graph.GraphChannel,TeamsManager.Core.Models.Channel,System.Boolean)", "methodShortName": "MapProperties(...)", "fileIndex": 0, "line": 27,
    "metrics": [
      { "value": 2970, "exceeded": true },
      { "value": 54, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "RemoveUsersFromTeamAsync()", "methodShortName": "RemoveUsersFromTeamAsync()", "fileIndex": 0, "line": 1798,
    "metrics": [
      { "value": 2970, "exceeded": true },
      { "value": 54, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ArchiveTeamAndDeactivateExclusiveUsersAsync()", "methodShortName": "ArchiveTeamAndDeactivateExclusiveUsersAsync()", "fileIndex": 0, "line": 818,
    "metrics": [
      { "value": 2756, "exceeded": true },
      { "value": 52, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkArchiveTeamsV2Async()", "methodShortName": "BulkArchiveTeamsV2Async()", "fileIndex": 0, "line": 1598,
    "metrics": [
      { "value": 2756, "exceeded": true },
      { "value": 52, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ArchiveTeamAndDeactivateExclusiveUsersAsync()", "methodShortName": "ArchiveTeamAndDeactivateExclusiveUsersAsync()", "fileIndex": 0, "line": 824,
    "metrics": [
      { "value": 2756, "exceeded": true },
      { "value": 52, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkArchiveTeamsV2Async()", "methodShortName": "BulkArchiveTeamsV2Async()", "fileIndex": 0, "line": 1604,
    "metrics": [
      { "value": 2756, "exceeded": true },
      { "value": 52, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryViewModel", "reportPath": "TeamsManager.UI_OperationHistoryViewModel.html", "methodName": "UpdateAvailableOperations()", "methodShortName": "UpdateAvailableOperations()", "fileIndex": 0, "line": 666,
    "metrics": [
      { "value": 2550, "exceeded": true },
      { "value": 50, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Extensions.EnumExtensions", "reportPath": "TeamsManager.Core_EnumExtensions.html", "methodName": "ToPolishEntityType(System.String)", "methodShortName": "ToPolishEntityType(...)", "fileIndex": 0, "line": 167,
    "metrics": [
      { "value": 2162, "exceeded": true },
      { "value": 46, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Extensions.EnumExtensions", "reportPath": "TeamsManager.Core_EnumExtensions.html", "methodName": "ToPolishEntityType(System.String)", "methodShortName": "ToPolishEntityType(...)", "fileIndex": 0, "line": 166,
    "metrics": [
      { "value": 2162, "exceeded": true },
      { "value": 46, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Data", "class": "TeamsManager.Data.Program", "reportPath": "TeamsManager.Data_Program.html", "methodName": "Main()", "methodShortName": "Main()", "fileIndex": 0, "line": 13,
    "metrics": [
      { "value": 2162, "exceeded": true },
      { "value": 46, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerHealthWidgetViewModel", "reportPath": "TeamsManager.UI_TeamsManagerHealthWidgetViewModel.html", "methodName": "ShowNotification(System.String,System.String)", "methodShortName": "ShowNotification(...)", "fileIndex": 0, "line": 445,
    "metrics": [
      { "value": 2162, "exceeded": true },
      { "value": 46, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "CreateTeamAsync()", "methodShortName": "CreateTeamAsync()", "fileIndex": 0, "line": 50,
    "metrics": [
      { "value": 1991, "exceeded": true },
      { "value": 64, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.GraphOperationResult<T>", "reportPath": "TeamsManager.Core_GraphOperationResult_1.html", "methodName": "GetDetailedResult()", "methodShortName": "GetDetailedResult()", "fileIndex": 0, "line": 338,
    "metrics": [
      { "value": 1980, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkUpdateUserPropertiesAsync()", "methodShortName": "BulkUpdateUserPropertiesAsync()", "fileIndex": 0, "line": 654,
    "metrics": [
      { "value": 1980, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkUpdateUserPropertiesAsync()", "methodShortName": "BulkUpdateUserPropertiesAsync()", "fileIndex": 0, "line": 660,
    "metrics": [
      { "value": 1980, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetAllTeamsAsync()", "methodShortName": "GetAllTeamsAsync()", "fileIndex": 0, "line": 460,
    "metrics": [
      { "value": 1980, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamAsync()", "methodShortName": "GetTeamAsync()", "fileIndex": 0, "line": 389,
    "metrics": [
      { "value": 1980, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetAllTeamsAsync()", "methodShortName": "GetAllTeamsAsync()", "fileIndex": 0, "line": 485,
    "metrics": [
      { "value": 1980, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.StringToBoolConverter", "reportPath": "TeamsManager.UI_StringToBoolConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 1980, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.TeamsManagerMonitoringService", "reportPath": "TeamsManager.UI_TeamsManagerMonitoringService.html", "methodName": "GetSystemHealthAsync()", "methodShortName": "GetSystemHealthAsync()", "fileIndex": 0, "line": 52,
    "metrics": [
      { "value": 1980, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "SynchronizeTeamMembershipAsync()", "methodShortName": "SynchronizeTeamMembershipAsync()", "fileIndex": 0, "line": 1026,
    "metrics": [
      { "value": 1806, "exceeded": true },
      { "value": 42, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "SynchronizeTeamMembershipAsync()", "methodShortName": "SynchronizeTeamMembershipAsync()", "fileIndex": 0, "line": 1032,
    "metrics": [
      { "value": 1806, "exceeded": true },
      { "value": 42, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "get_Item(System.String)", "methodShortName": "get_Item(...)", "fileIndex": 0, "line": 247,
    "metrics": [
      { "value": 1806, "exceeded": true },
      { "value": 42, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "UpdateTemplateAsync()", "methodShortName": "UpdateTemplateAsync()", "fileIndex": 0, "line": 385,
    "metrics": [
      { "value": 1482, "exceeded": true },
      { "value": 38, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "reportPath": "TeamsManager.UI_TeamCreationWizardViewModel.html", "methodName": "GetPlaceholderDisplayName(System.String)", "methodShortName": "GetPlaceholderDisplayName(...)", "fileIndex": 0, "line": 373,
    "metrics": [
      { "value": 1482, "exceeded": true },
      { "value": 38, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "reportPath": "TeamsManager.UI_TeamCreationWizardViewModel.html", "methodName": "GetPlaceholderDescription(System.String)", "methodShortName": "GetPlaceholderDescription(...)", "fileIndex": 0, "line": 389,
    "metrics": [
      { "value": 1482, "exceeded": true },
      { "value": 38, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.DiagnosticsController", "reportPath": "TeamsManager.Api_DiagnosticsController.html", "methodName": "TestGraphApiAsync()", "methodShortName": "TestGraphApiAsync()", "fileIndex": 0, "line": 594,
    "metrics": [
      { "value": 1332, "exceeded": true },
      { "value": 36, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphExceptionHandler", "reportPath": "TeamsManager.Core_GraphExceptionHandler.html", "methodName": "HandleGraphConnectionExceptionAsync()", "methodShortName": "HandleGraphConnectionExceptionAsync()", "fileIndex": 0, "line": 48,
    "metrics": [
      { "value": 1332, "exceeded": true },
      { "value": 36, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamMembersAsync()", "methodShortName": "GetTeamMembersAsync()", "fileIndex": 0, "line": 601,
    "metrics": [
      { "value": 1332, "exceeded": true },
      { "value": 36, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamMembersAsync()", "methodShortName": "GetTeamMembersAsync()", "fileIndex": 0, "line": 626,
    "metrics": [
      { "value": 1332, "exceeded": true },
      { "value": 36, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.GraphSynchronizerBase<T1, T2>", "reportPath": "TeamsManager.Core_GraphSynchronizerBase_2.html", "methodName": "GetPropertyValue(TGraphModel,System.String,TValue)", "methodShortName": "GetPropertyValue(...)", "fileIndex": 0, "line": 130,
    "metrics": [
      { "value": 1332, "exceeded": true },
      { "value": 36, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Configuration.ConfigurationValidator", "reportPath": "TeamsManager.UI_ConfigurationValidator.html", "methodName": "ValidateOAuthConfigurationAsync()", "methodShortName": "ValidateOAuthConfigurationAsync()", "fileIndex": 0, "line": 89,
    "metrics": [
      { "value": 1332, "exceeded": true },
      { "value": 36, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphExceptionHandler", "reportPath": "TeamsManager.Core_GraphExceptionHandler.html", "methodName": "HandleGraphApiExceptionAsync()", "methodShortName": "HandleGraphApiExceptionAsync()", "fileIndex": 0, "line": 212,
    "metrics": [
      { "value": 1190, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "MapToGraphUser(System.Text.Json.JsonElement)", "methodShortName": "MapToGraphUser(...)", "fileIndex": 0, "line": 1690,
    "metrics": [
      { "value": 1190, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.TeamSynchronizer", "reportPath": "TeamsManager.Core_TeamSynchronizer.html", "methodName": "MapProperties(TeamsManager.Core.Models.Graph.GraphTeam,TeamsManager.Core.Models.Team,System.Boolean)", "methodShortName": "MapProperties(...)", "fileIndex": 0, "line": 28,
    "metrics": [
      { "value": 1190, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.TeamsManagerMonitoringService", "reportPath": "TeamsManager.UI_TeamsManagerMonitoringService.html", "methodName": "GetPerformanceMetricsAsync()", "methodShortName": "GetPerformanceMetricsAsync()", "fileIndex": 0, "line": 161,
    "metrics": [
      { "value": 1190, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "System.Windows.Markup.IComponentConnector.Connect(System.Int32,System.Object)", "methodShortName": "System.Windows.Markup.IComponentConnector.Connect(...)", "fileIndex": 0, "line": 60,
    "metrics": [
      { "value": 1190, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "BulkTeamMembershipOperationAsync()", "methodShortName": "BulkTeamMembershipOperationAsync()", "fileIndex": 0, "line": 397,
    "metrics": [
      { "value": 1056, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "MigrateTeamsBetweenSchoolYearsAsync()", "methodShortName": "MigrateTeamsBetweenSchoolYearsAsync()", "fileIndex": 0, "line": 527,
    "metrics": [
      { "value": 1056, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "MigrateTeamsBetweenSchoolYearsAsync()", "methodShortName": "MigrateTeamsBetweenSchoolYearsAsync()", "fileIndex": 0, "line": 512,
    "metrics": [
      { "value": 1056, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphConnectionService", "reportPath": "TeamsManager.Core_GraphConnectionService.html", "methodName": "GetDiagnosticInfoAsync()", "methodShortName": "GetDiagnosticInfoAsync()", "fileIndex": 0, "line": 225,
    "metrics": [
      { "value": 1056, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "CreateTeamChannelAsync()", "methodShortName": "CreateTeamChannelAsync()", "fileIndex": 0, "line": 1158,
    "metrics": [
      { "value": 1056, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamChannelsAsync()", "methodShortName": "GetTeamChannelsAsync()", "fileIndex": 0, "line": 1347,
    "metrics": [
      { "value": 1056, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "CreateTeamChannelAsync()", "methodShortName": "CreateTeamChannelAsync()", "fileIndex": 0, "line": 1183,
    "metrics": [
      { "value": 1056, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamChannelsAsync()", "methodShortName": "GetTeamChannelsAsync()", "fileIndex": 0, "line": 1372,
    "metrics": [
      { "value": 1056, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamChannelsViewModel", "reportPath": "TeamsManager.UI_TeamChannelsViewModel.html", "methodName": "ImportChannelsFromCsvAsync()", "methodShortName": "ImportChannelsFromCsvAsync()", "fileIndex": 0, "line": 383,
    "metrics": [
      { "value": 1056, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamAsync()", "methodShortName": "GetTeamAsync()", "fileIndex": 0, "line": 409,
    "metrics": [
      { "value": 1052, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ExecuteBatchWithRetryAsync()", "methodShortName": "ExecuteBatchWithRetryAsync()", "fileIndex": 0, "line": 2022,
    "metrics": [
      { "value": 930, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ExecuteBatchWithRetryAsync()", "methodShortName": "ExecuteBatchWithRetryAsync()", "fileIndex": 0, "line": 2058,
    "metrics": [
      { "value": 930, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "GetTtlStats()", "methodShortName": "GetTtlStats()", "fileIndex": 0, "line": 885,
    "metrics": [
      { "value": 930, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphConnectionService", "reportPath": "TeamsManager.Core_GraphConnectionService.html", "methodName": "GetPermissionInfoAsync()", "methodShortName": "GetPermissionInfoAsync()", "fileIndex": 0, "line": 380,
    "metrics": [
      { "value": 930, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "GetPropertyValue(System.Object,System.String)", "methodShortName": "GetPropertyValue(...)", "fileIndex": 0, "line": 1419,
    "metrics": [
      { "value": 930, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "RemoveTeacherFromSubjectAsync()", "methodShortName": "RemoveTeacherFromSubjectAsync()", "fileIndex": 0, "line": 1096,
    "metrics": [
      { "value": 930, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "RemoveUserFromSchoolTypeAsync()", "methodShortName": "RemoveUserFromSchoolTypeAsync()", "fileIndex": 0, "line": 840,
    "metrics": [
      { "value": 930, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerMetricsWidgetViewModel", "reportPath": "TeamsManager.UI_TeamsManagerMetricsWidgetViewModel.html", "methodName": "CheckMetricsNotifications(System.Object)", "methodShortName": "CheckMetricsNotifications(...)", "fileIndex": 0, "line": 316,
    "metrics": [
      { "value": 930, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "GenerateOrganizationalUnitCodeAsync()", "methodShortName": "GenerateOrganizationalUnitCodeAsync()", "fileIndex": 0, "line": 1031,
    "metrics": [
      { "value": 930, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Hubs.MonitoringHub", "reportPath": "TeamsManager.Api_MonitoringHub.html", "methodName": "OnConnectedAsync()", "methodShortName": "OnConnectedAsync()", "fileIndex": 0, "line": 37,
    "metrics": [
      { "value": 812, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "HandleGraphConnectionExceptionAsync()", "methodShortName": "HandleGraphConnectionExceptionAsync()", "fileIndex": 0, "line": 2094,
    "metrics": [
      { "value": 812, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "GetTemplateByIdAsync()", "methodShortName": "GetTemplateByIdAsync()", "fileIndex": 0, "line": 70,
    "metrics": [
      { "value": 812, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.ConditionalAccessAnalyzer", "reportPath": "TeamsManager.UI_ConditionalAccessAnalyzer.html", "methodName": "AnalyzeToken(Microsoft.Identity.Client.AuthenticationResult)", "methodShortName": "AnalyzeToken(...)", "fileIndex": 0, "line": 27,
    "metrics": [
      { "value": 812, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryViewModel", "reportPath": "TeamsManager.UI_OperationHistoryViewModel.html", "methodName": "ApplyFilters()", "methodShortName": "ApplyFilters()", "fileIndex": 0, "line": 570,
    "metrics": [
      { "value": 812, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectEditViewModel", "reportPath": "TeamsManager.UI_SubjectEditViewModel.html", "methodName": "get_Item(System.String)", "methodShortName": "get_Item(...)", "fileIndex": 0, "line": 274,
    "metrics": [
      { "value": 812, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "reportPath": "TeamsManager.UI_UserListViewModel.html", "methodName": "OnFilter(System.Object,System.Windows.Data.FilterEventArgs)", "methodShortName": "OnFilter(...)", "fileIndex": 0, "line": 373,
    "metrics": [
      { "value": 812, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ApplicationSettingService", "reportPath": "TeamsManager.Core_ApplicationSettingService.html", "methodName": "GetSettingValueAsync()", "methodShortName": "GetSettingValueAsync()", "fileIndex": 0, "line": 95,
    "metrics": [
      { "value": 756, "exceeded": true },
      { "value": 27, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ApplicationSettingService", "reportPath": "TeamsManager.Core_ApplicationSettingService.html", "methodName": "SaveSettingAsync()", "methodShortName": "SaveSettingAsync()", "fileIndex": 0, "line": 196,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Cache.CacheInvalidationService", "reportPath": "TeamsManager.Core_CacheInvalidationService.html", "methodName": "InvalidateForTeamUpdatedAsync()", "methodShortName": "InvalidateForTeamUpdatedAsync()", "fileIndex": 0, "line": 58,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkArchiveTeamsAsync()", "methodShortName": "BulkArchiveTeamsAsync()", "fileIndex": 0, "line": 350,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapTeamSettings(System.Object)", "methodShortName": "MapTeamSettings(...)", "fileIndex": 0, "line": 1705,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapMessagingSettings(System.Object)", "methodShortName": "MapMessagingSettings(...)", "fileIndex": 0, "line": 1751,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapFunSettings(System.Object)", "methodShortName": "MapFunSettings(...)", "fileIndex": 0, "line": 1768,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapTeamSettings(System.Object)", "methodShortName": "MapTeamSettings(...)", "fileIndex": 0, "line": 1730,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapMessagingSettings(System.Object)", "methodShortName": "MapMessagingSettings(...)", "fileIndex": 0, "line": 1776,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapFunSettings(System.Object)", "methodShortName": "MapFunSettings(...)", "fileIndex": 0, "line": 1793,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.TimeSpanToReadableConverter", "reportPath": "TeamsManager.UI_TimeSpanToReadableConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 13,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "get_CurrentSemester()", "methodShortName": "get_CurrentSemester()", "fileIndex": 0, "line": 231,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "reportPath": "TeamsManager.UI_UniversalDialogViewModel.html", "methodName": "CalculateOptimalMaxWidth()", "methodShortName": "CalculateOptimalMaxWidth()", "fileIndex": 0, "line": 170,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerMetricsWidgetViewModel", "reportPath": "TeamsManager.UI_TeamsManagerMetricsWidgetViewModel.html", "methodName": "CheckRateLimitNotifications(System.Object)", "methodShortName": "CheckRateLimitNotifications(...)", "fileIndex": 0, "line": 300,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Shell.MainShellViewModel", "reportPath": "TeamsManager.UI_MainShellViewModel.html", "methodName": "LoadDetailedUserProfileAsync()", "methodShortName": "LoadDetailedUserProfileAsync()", "fileIndex": 0, "line": 341,
    "metrics": [
      { "value": 702, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "Program", "reportPath": "TeamsManager.Api_Program.html", "methodName": "<Main>$(System.String[])", "methodShortName": "<Main>$(...)", "fileIndex": 0, "line": 45,
    "metrics": [
      { "value": 642, "exceeded": true },
      { "value": 82, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "UpdateM365UserPropertiesAsync()", "methodShortName": "UpdateM365UserPropertiesAsync()", "fileIndex": 0, "line": 391,
    "metrics": [
      { "value": 640, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphConnectionService", "reportPath": "TeamsManager.Core_GraphConnectionService.html", "methodName": "TestConnectionAsync()", "methodShortName": "TestConnectionAsync()", "fileIndex": 0, "line": 543,
    "metrics": [
      { "value": 600, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.GraphAdminNotificationService", "reportPath": "TeamsManager.Core_GraphAdminNotificationService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.Graph.IGraphService,TeamsManager.Core.Abstractions.Services.IModernHttpService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.GraphAdminNotificationService>,Microsoft.Extensions.Configuration.IConfiguration)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 29,
    "metrics": [
      { "value": 600, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "UpdateOrganizationalUnitAsync()", "methodShortName": "UpdateOrganizationalUnitAsync()", "fileIndex": 0, "line": 356,
    "metrics": [
      { "value": 600, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SubjectService", "reportPath": "TeamsManager.Core_SubjectService.html", "methodName": "UpdateSubjectAsync()", "methodShortName": "UpdateSubjectAsync()", "fileIndex": 0, "line": 263,
    "metrics": [
      { "value": 600, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.App", "reportPath": "TeamsManager.UI_App.html", "methodName": "ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection)", "methodShortName": "ConfigureServices(...)", "fileIndex": 1, "line": 55,
    "metrics": [
      { "value": 600, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypeEditViewModel", "reportPath": "TeamsManager.UI_SchoolTypeEditViewModel.html", "methodName": "get_Item(System.String)", "methodShortName": "get_Item(...)", "fileIndex": 0, "line": 242,
    "metrics": [
      { "value": 600, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectImportViewModel", "reportPath": "TeamsManager.UI_SubjectImportViewModel.html", "methodName": "CreateSubjectPreview(System.String[])", "methodShortName": "CreateSubjectPreview(...)", "fileIndex": 0, "line": 373,
    "metrics": [
      { "value": 600, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel", "reportPath": "TeamsManager.UI_UserSchoolTypeAssignmentViewModel.html", "methodName": "SaveAssignmentAsync()", "methodShortName": "SaveAssignmentAsync()", "fileIndex": 0, "line": 308,
    "metrics": [
      { "value": 600, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "GetUserByIdAsync()", "methodShortName": "GetUserByIdAsync()", "fileIndex": 0, "line": 92,
    "metrics": [
      { "value": 520, "exceeded": true },
      { "value": 36, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.BulkUserManagementController", "reportPath": "TeamsManager.Api_BulkUserManagementController.html", "methodName": "BulkUserOffboarding()", "methodShortName": "BulkUserOffboarding()", "fileIndex": 0, "line": 107,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.DiagnosticsController", "reportPath": "TeamsManager.Api_DiagnosticsController.html", "methodName": "GetGraphStatusAsync()", "methodShortName": "GetGraphStatusAsync()", "fileIndex": 0, "line": 512,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Swagger.AuthorizationOperationFilter", "reportPath": "TeamsManager.Api_AuthorizationOperationFilter.html", "methodName": "Apply(Microsoft.OpenApi.Models.OpenApiOperation,Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext)", "methodShortName": "Apply(...)", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ExecuteBatchOperationsAsync()", "methodShortName": "ExecuteBatchOperationsAsync()", "fileIndex": 0, "line": 1800,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ProcessUpdateBatchAsync()", "methodShortName": "ProcessUpdateBatchAsync()", "fileIndex": 0, "line": 1949,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ProcessUpdateBatchAsync()", "methodShortName": "ProcessUpdateBatchAsync()", "fileIndex": 0, "line": 1985,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphConnectionService", "reportPath": "TeamsManager.Core_GraphConnectionService.html", "methodName": "AnalyzeGraphError(System.Exception)", "methodShortName": "AnalyzeGraphError(...)", "fileIndex": 0, "line": 1075,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "UpdateTeamPropertiesAsync()", "methodShortName": "UpdateTeamPropertiesAsync()", "fileIndex": 0, "line": 160,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.UserSynchronizer", "reportPath": "TeamsManager.Core_UserSynchronizer.html", "methodName": "MapProperties(TeamsManager.Core.Models.Graph.GraphUser,TeamsManager.Core.Models.User,System.Boolean)", "methodShortName": "MapProperties(...)", "fileIndex": 0, "line": 28,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "CreateTemplateAsync()", "methodShortName": "CreateTemplateAsync()", "fileIndex": 0, "line": 223,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "UpdateUserAsync()", "methodShortName": "UpdateUserAsync()", "fileIndex": 0, "line": 507,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.LoginViewModel", "reportPath": "TeamsManager.UI_LoginViewModel.html", "methodName": "LoginAsync()", "methodShortName": "LoginAsync()", "fileIndex": 0, "line": 159,
    "metrics": [
      { "value": 506, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "CreateTeamAsync()", "methodShortName": "CreateTeamAsync()", "fileIndex": 0, "line": 305,
    "metrics": [
      { "value": 437, "exceeded": true },
      { "value": 58, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.BulkUserManagementController", "reportPath": "TeamsManager.Api_BulkUserManagementController.html", "methodName": "BulkRoleChange()", "methodShortName": "BulkRoleChange()", "fileIndex": 0, "line": 170,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.BulkUserManagementController", "reportPath": "TeamsManager.Api_BulkUserManagementController.html", "methodName": "BulkTeamMembershipOperation()", "methodShortName": "BulkTeamMembershipOperation()", "fileIndex": 0, "line": 232,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.BulkUserManagementController", "reportPath": "TeamsManager.Api_BulkUserManagementController.html", "methodName": "BulkUserOnboarding()", "methodShortName": "BulkUserOnboarding()", "fileIndex": 0, "line": 45,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Swagger.TagsDocumentFilter", "reportPath": "TeamsManager.Api_TagsDocumentFilter.html", "methodName": "Apply(Microsoft.OpenApi.Models.OpenApiDocument,Swashbuckle.AspNetCore.SwaggerGen.DocumentFilterContext)", "methodShortName": "Apply(...)", "fileIndex": 0, "line": 12,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.DataImportOrchestrator", "reportPath": "TeamsManager.Application_DataImportOrchestrator.html", "methodName": "MapCsvRowToUserData(System.String[],System.String[],System.Collections.Generic.Dictionary`2<System.String,System.String>)", "methodShortName": "MapCsvRowToUserData(...)", "fileIndex": 0, "line": 152,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "reportPath": "TeamsManager.Core_GraphApiException.html", "methodName": "GetDetailedErrorMessage()", "methodShortName": "GetDetailedErrorMessage()", "fileIndex": 0, "line": 209,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphRateLimitException", "reportPath": "TeamsManager.Core_GraphRateLimitException.html", "methodName": "GetDetailedErrorMessage()", "methodShortName": "GetDetailedErrorMessage()", "fileIndex": 0, "line": 182,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ApplicationSettingService", "reportPath": "TeamsManager.Core_ApplicationSettingService.html", "methodName": "UpdateSettingAsync()", "methodShortName": "UpdateSettingAsync()", "fileIndex": 0, "line": 310,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphExceptionHandler", "reportPath": "TeamsManager.Core_GraphExceptionHandler.html", "methodName": "HandleGraphRateLimitExceptionAsync()", "methodShortName": "HandleGraphRateLimitExceptionAsync()", "fileIndex": 0, "line": 364,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "WaitForTeamCreationAsync()", "methodShortName": "WaitForTeamCreationAsync()", "fileIndex": 0, "line": 1538,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "WaitForTeamCreationAsync()", "methodShortName": "WaitForTeamCreationAsync()", "fileIndex": 0, "line": 1563,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "DeleteOrganizationalUnitAsync()", "methodShortName": "DeleteOrganizationalUnitAsync()", "fileIndex": 0, "line": 554,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "MoveDepartmentsToOrganizationalUnitAsync()", "methodShortName": "MoveDepartmentsToOrganizationalUnitAsync()", "fileIndex": 0, "line": 772,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.DateTimeToRelativeConverter", "reportPath": "TeamsManager.UI_DateTimeToRelativeConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 96,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "GenerateDepartmentCodeAsync()", "methodShortName": "GenerateDepartmentCodeAsync()", "fileIndex": 0, "line": 823,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingsViewModel", "reportPath": "TeamsManager.UI_ApplicationSettingsViewModel.html", "methodName": "FilterSettings(System.Object)", "methodShortName": "FilterSettings(...)", "fileIndex": 0, "line": 249,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Shell.MainShellViewModel", "reportPath": "TeamsManager.UI_MainShellViewModel.html", "methodName": "CheckAutoLoginAsync()", "methodShortName": "CheckAutoLoginAsync()", "fileIndex": 0, "line": 437,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamChannelsViewModel", "reportPath": "TeamsManager.UI_TeamChannelsViewModel.html", "methodName": "LoadChannelsAsync()", "methodShortName": "LoadChannelsAsync()", "fileIndex": 0, "line": 237,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "reportPath": "TeamsManager.UI_TeamCreationWizardViewModel.html", "methodName": "UpdateGeneratedName()", "methodShortName": "UpdateGeneratedName()", "fileIndex": 0, "line": 405,
    "metrics": [
      { "value": 420, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.ManualTestingService", "reportPath": "TeamsManager.UI_ManualTestingService.html", "methodName": "GenerateMarkdownReport(TeamsManager.UI.Models.TestSuite)", "methodShortName": "GenerateMarkdownReport(...)", "fileIndex": 0, "line": 330,
    "metrics": [
      { "value": 380, "exceeded": true },
      { "value": 19, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "BulkRestoreTeamsWithValidationAsync()", "methodShortName": "BulkRestoreTeamsWithValidationAsync()", "fileIndex": 0, "line": 300,
    "metrics": [
      { "value": 361, "exceeded": true },
      { "value": 36, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.DiagnosticsController", "reportPath": "TeamsManager.Api_DiagnosticsController.html", "methodName": "GetGraphPermissionsAsync()", "methodShortName": "GetGraphPermissionsAsync()", "fileIndex": 0, "line": 857,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.DataImportOrchestrator", "reportPath": "TeamsManager.Application_DataImportOrchestrator.html", "methodName": "ProcessUserBatchAsync()", "methodShortName": "ProcessUserBatchAsync()", "fileIndex": 0, "line": 240,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "PerformGraphCleanupOperationsAsync()", "methodShortName": "PerformGraphCleanupOperationsAsync()", "fileIndex": 0, "line": 987,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "PerformGraphCleanupOperationsAsync()", "methodShortName": "PerformGraphCleanupOperationsAsync()", "fileIndex": 0, "line": 972,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "reportPath": "TeamsManager.Core_GraphApiException.html", "methodName": "CanRetry()", "methodShortName": "CanRetry()", "fileIndex": 0, "line": 132,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.GraphBulkResult", "reportPath": "TeamsManager.Core_GraphBulkResult.html", "methodName": "GetDetailedResult()", "methodShortName": "GetDetailedResult()", "fileIndex": 0, "line": 293,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapMemberSettings(System.Object)", "methodShortName": "MapMemberSettings(...)", "fileIndex": 0, "line": 1736,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamChannelByIdAsync()", "methodShortName": "GetTeamChannelByIdAsync()", "fileIndex": 0, "line": 1438,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapMemberSettings(System.Object)", "methodShortName": "MapMemberSettings(...)", "fileIndex": 0, "line": 1761,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamChannelByIdAsync()", "methodShortName": "GetTeamChannelByIdAsync()", "fileIndex": 0, "line": 1463,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "CanMoveUnitAsync()", "methodShortName": "CanMoveUnitAsync()", "fileIndex": 0, "line": 891,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.TeamSynchronizer", "reportPath": "TeamsManager.Core_TeamSynchronizer.html", "methodName": "GetPropertyValueFromObject(System.Object,System.String,TValue)", "methodShortName": "GetPropertyValueFromObject(...)", "fileIndex": 0, "line": 222,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.GreaterThanConverter", "reportPath": "TeamsManager.UI_GreaterThanConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringDataService", "reportPath": "TeamsManager.UI_MonitoringDataService.html", "methodName": "ConvertGraphHealthStatusToUI(System.String)", "methodShortName": "ConvertGraphHealthStatusToUI(...)", "fileIndex": 0, "line": 436,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerHealthWidgetViewModel", "reportPath": "TeamsManager.UI_TeamsManagerHealthWidgetViewModel.html", "methodName": "RefreshAsync()", "methodShortName": "RefreshAsync()", "fileIndex": 0, "line": 79,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryViewModel", "reportPath": "TeamsManager.UI_OperationHistoryViewModel.html", "methodName": "GetEntityTypeFromOperationType(System.String)", "methodShortName": "GetEntityTypeFromOperationType(...)", "fileIndex": 0, "line": 646,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "PrepareModelForSave()", "methodShortName": "PrepareModelForSave()", "fileIndex": 0, "line": 890,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingItemViewModel", "reportPath": "TeamsManager.UI_ApplicationSettingItemViewModel.html", "methodName": "ValidateType()", "methodShortName": "ValidateType()", "fileIndex": 0, "line": 327,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectsViewModel", "reportPath": "TeamsManager.UI_SubjectsViewModel.html", "methodName": "FilterSubjects(System.Object)", "methodShortName": "FilterSubjects(...)", "fileIndex": 0, "line": 179,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.ChannelCardViewModel", "reportPath": "TeamsManager.UI_ChannelCardViewModel.html", "methodName": "SaveChangesAsync()", "methodShortName": "SaveChangesAsync()", "fileIndex": 0, "line": 233,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.ChannelCardViewModel", "reportPath": "TeamsManager.UI_ChannelCardViewModel.html", "methodName": "ToggleArchiveAsync()", "methodShortName": "ToggleArchiveAsync()", "fileIndex": 0, "line": 353,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "ExecuteGraphApiTest()", "methodShortName": "ExecuteGraphApiTest()", "fileIndex": 1, "line": 674,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "SaveTestSessionToMarkdown()", "methodShortName": "SaveTestSessionToMarkdown()", "fileIndex": 1, "line": 850,
    "metrics": [
      { "value": 342, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.DiagnosticsController", "reportPath": "TeamsManager.Api_DiagnosticsController.html", "methodName": "GeneratePermissionRecommendations(TeamsManager.Core.Models.Graph.GraphPermissionInfo,System.String[],System.String[],System.String[],System.String[])", "methodShortName": "GeneratePermissionRecommendations(...)", "fileIndex": 0, "line": 974,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Helpers.GraphModelMapper", "reportPath": "TeamsManager.Core_GraphModelMapper.html", "methodName": "GetDateTime(System.Object,System.String)", "methodShortName": "GetDateTime(...)", "fileIndex": 0, "line": 78,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ArchiveTeamsAsync()", "methodShortName": "ArchiveTeamsAsync()", "fileIndex": 0, "line": 450,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ArchiveTeamsAsync()", "methodShortName": "ArchiveTeamsAsync()", "fileIndex": 0, "line": 456,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "<WaitForTeamCreationAsync()", "methodShortName": "<WaitForTeamCreationAsync()", "fileIndex": 0, "line": 1580,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "AddTeamMemberAsync()", "methodShortName": "AddTeamMemberAsync()", "fileIndex": 0, "line": 734,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "UpdateTeamChannelAsync()", "methodShortName": "UpdateTeamChannelAsync()", "fileIndex": 0, "line": 1237,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "<WaitForTeamCreationAsync()", "methodShortName": "<WaitForTeamCreationAsync()", "fileIndex": 0, "line": 1605,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "AddTeamMemberAsync()", "methodShortName": "AddTeamMemberAsync()", "fileIndex": 0, "line": 759,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "UpdateTeamChannelAsync()", "methodShortName": "UpdateTeamChannelAsync()", "fileIndex": 0, "line": 1262,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "CreateOrganizationalUnitAsync()", "methodShortName": "CreateOrganizationalUnitAsync()", "fileIndex": 0, "line": 223,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolTypeService", "reportPath": "TeamsManager.Core_SchoolTypeService.html", "methodName": "AssignViceDirectorToSchoolTypeAsync()", "methodShortName": "AssignViceDirectorToSchoolTypeAsync()", "fileIndex": 0, "line": 463,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolTypeService", "reportPath": "TeamsManager.Core_SchoolTypeService.html", "methodName": "UpdateSchoolTypeAsync()", "methodShortName": "UpdateSchoolTypeAsync()", "fileIndex": 0, "line": 230,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.ChannelSynchronizer", "reportPath": "TeamsManager.Core_ChannelSynchronizer.html", "methodName": "DetectChangesAsync()", "methodShortName": "DetectChangesAsync()", "fileIndex": 0, "line": 108,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "<AddUsersToTeamAsync()", "methodShortName": "<AddUsersToTeamAsync()", "fileIndex": 0, "line": 1740,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "<RemoveUsersFromTeamAsync()", "methodShortName": "<RemoveUsersFromTeamAsync()", "fileIndex": 0, "line": 1950,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "AddMemberAsync()", "methodShortName": "AddMemberAsync()", "fileIndex": 0, "line": 1023,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "ProcessAddUsersInSmallerBatchesAsync()", "methodShortName": "ProcessAddUsersInSmallerBatchesAsync()", "fileIndex": 0, "line": 2222,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "ProcessRemoveUsersInSmallerBatchesAsync()", "methodShortName": "ProcessRemoveUsersInSmallerBatchesAsync()", "fileIndex": 0, "line": 2279,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "SynchronizeAllTeamsAsync()", "methodShortName": "SynchronizeAllTeamsAsync()", "fileIndex": 0, "line": 1457,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "ActivateUserAsync()", "methodShortName": "ActivateUserAsync()", "fileIndex": 0, "line": 1430,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.EnumDescriptionConverter", "reportPath": "TeamsManager.UI_EnumDescriptionConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 16,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.EnumDescriptionConverter", "reportPath": "TeamsManager.UI_EnumDescriptionConverter.html", "methodName": "ConvertBack(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "ConvertBack(...)", "fileIndex": 0, "line": 38,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Configuration.ConfigurationValidator", "reportPath": "TeamsManager.UI_ConfigurationValidator.html", "methodName": "ValidateApiConfigurationAsync()", "methodShortName": "ValidateApiConfigurationAsync()", "fileIndex": 0, "line": 199,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringDataService", "reportPath": "TeamsManager.UI_MonitoringDataService.html", "methodName": "ConvertGraphDiagnosticInfoToSystemHealth(TeamsManager.Core.Models.Graph.GraphDiagnosticInfo)", "methodShortName": "ConvertGraphDiagnosticInfoToSystemHealth(...)", "fileIndex": 0, "line": 376,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.SystemHealthDataComparer", "reportPath": "TeamsManager.UI_SystemHealthDataComparer.html", "methodName": "Equals(TeamsManager.UI.Models.Monitoring.SystemHealthData,TeamsManager.UI.Models.Monitoring.SystemHealthData)", "methodShortName": "Equals(...)", "fileIndex": 0, "line": 408,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.SchoolTypeUIService", "reportPath": "TeamsManager.UI_SchoolTypeUIService.html", "methodName": "CreateSchoolTypeAsync()", "methodShortName": "CreateSchoolTypeAsync()", "fileIndex": 0, "line": 83,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": "UpdateCommandStates()", "methodShortName": "UpdateCommandStates()", "fileIndex": 0, "line": 536,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.BulkImportWizardViewModel", "reportPath": "TeamsManager.UI_BulkImportWizardViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IDataImportOrchestrator,TeamsManager.Core.Abstractions.Services.Auth.ITokenManager,TeamsManager.UI.ViewModels.Import.ImportFileSelectionViewModel,TeamsManager.UI.ViewModels.Import.ImportColumnMappingViewModel,TeamsManager.UI.ViewModels.Import.ImportValidationViewModel,TeamsManager.UI.ViewModels.Import.ImportProgressViewModel,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Import.BulkImportWizardViewModel>,TeamsManager.UI.Services.Abstractions.IUIDialogService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 26,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportColumnMappingViewModel", "reportPath": "TeamsManager.UI_ImportColumnMappingViewModel.html", "methodName": "DetectColumnsInFile()", "methodShortName": "DetectColumnsInFile()", "fileIndex": 0, "line": 120,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Shell.MainShellViewModel", "reportPath": "TeamsManager.UI_MainShellViewModel.html", "methodName": ".ctor(System.IServiceProvider,TeamsManager.Core.Abstractions.ICurrentUserService,TeamsManager.UI.Services.Abstractions.IMsalAuthService,TeamsManager.UI.Services.Configuration.ConfigurationManager,TeamsManager.UI.Services.Abstractions.IGraphUserProfileService,TeamsManager.UI.Services.ConditionalAccessAnalyzer,TeamsManager.UI.Services.IUserSynchronizationService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Shell.MainShellViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 34,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.ChannelCardViewModel", "reportPath": "TeamsManager.UI_ChannelCardViewModel.html", "methodName": "DeleteChannelAsync()", "methodShortName": "DeleteChannelAsync()", "fileIndex": 0, "line": 298,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "reportPath": "TeamsManager.UI_TeamCreationWizardViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.ITeamService,TeamsManager.Core.Abstractions.Services.ITeamTemplateService,TeamsManager.Core.Abstractions.Services.IUserService,TeamsManager.Core.Abstractions.Services.ISchoolTypeService,TeamsManager.Core.Abstractions.Services.ISchoolYearService,TeamsManager.Core.Abstractions.ICurrentUserService,TeamsManager.Core.Abstractions.Services.INotificationService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 48,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "reportPath": "TeamsManager.UI_TeamCreationWizardViewModel.html", "methodName": "CreateTeam()", "methodShortName": "CreateTeam()", "fileIndex": 0, "line": 513,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamLifecycleDialogViewModel", "reportPath": "TeamsManager.UI_TeamLifecycleDialogViewModel.html", "methodName": "ExecuteOperationAsync()", "methodShortName": "ExecuteOperationAsync()", "fileIndex": 0, "line": 443,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamListViewModel", "reportPath": "TeamsManager.UI_TeamListViewModel.html", "methodName": "ArchiveTeamAsync()", "methodShortName": "ArchiveTeamAsync()", "fileIndex": 0, "line": 274,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamListViewModel", "reportPath": "TeamsManager.UI_TeamListViewModel.html", "methodName": "LoadTeamsAsync()", "methodShortName": "LoadTeamsAsync()", "fileIndex": 0, "line": 200,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamListViewModel", "reportPath": "TeamsManager.UI_TeamListViewModel.html", "methodName": "RestoreTeamAsync()", "methodShortName": "RestoreTeamAsync()", "fileIndex": 0, "line": 334,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamMembersViewModel", "reportPath": "TeamsManager.UI_TeamMembersViewModel.html", "methodName": "RemoveSpecificMemberAsync()", "methodShortName": "RemoveSpecificMemberAsync()", "fileIndex": 0, "line": 262,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "HamburgerButton_Click(System.Object,System.Windows.RoutedEventArgs)", "methodShortName": "HamburgerButton_Click(...)", "fileIndex": 1, "line": 400,
    "metrics": [
      { "value": 272, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "UpdateTeamPropertiesAsync()", "methodShortName": "UpdateTeamPropertiesAsync()", "fileIndex": 0, "line": 165,
    "metrics": [
      { "value": 267, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringDataService", "reportPath": "TeamsManager.UI_MonitoringDataService.html", "methodName": "GetRecentAlertsAsync()", "methodShortName": "GetRecentAlertsAsync()", "fileIndex": 0, "line": 200,
    "metrics": [
      { "value": 261, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "BulkRestoreTeamsWithValidationAsync()", "methodShortName": "BulkRestoreTeamsWithValidationAsync()", "fileIndex": 0, "line": 305,
    "metrics": [
      { "value": 230, "exceeded": true },
      { "value": 40, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringDataService", "reportPath": "TeamsManager.UI_MonitoringDataService.html", "methodName": "ConvertStringToOperationStatus(System.String)", "methodShortName": "ConvertStringToOperationStatus(...)", "fileIndex": 0, "line": 520,
    "metrics": [
      { "value": 226, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.DataImportOrchestrator", "reportPath": "TeamsManager.Application_DataImportOrchestrator.html", "methodName": "ValidateUsersBusinessRulesAsync()", "methodShortName": "ValidateUsersBusinessRulesAsync()", "fileIndex": 0, "line": 494,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Cache.CacheInvalidationService", "reportPath": "TeamsManager.Core_CacheInvalidationService.html", "methodName": "InvalidateForUserDeactivatedAsync()", "methodShortName": "InvalidateForUserDeactivatedAsync()", "fileIndex": 0, "line": 280,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "CreateTeamsAsync()", "methodShortName": "CreateTeamsAsync()", "fileIndex": 0, "line": 553,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "CreateTeamsAsync()", "methodShortName": "CreateTeamsAsync()", "fileIndex": 0, "line": 559,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "GetExpiringEntries(System.TimeSpan)", "methodShortName": "GetExpiringEntries(...)", "fileIndex": 0, "line": 764,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "ValidateCache(System.String,System.String)", "methodShortName": "ValidateCache(...)", "fileIndex": 0, "line": 1182,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphConnectionService", "reportPath": "TeamsManager.Core_GraphConnectionService.html", "methodName": "CheckEndpointAvailabilityAsync()", "methodShortName": "CheckEndpointAvailabilityAsync()", "fileIndex": 0, "line": 735,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapGuestSettings(System.Object)", "methodShortName": "MapGuestSettings(...)", "fileIndex": 0, "line": 1722,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapGuestSettings(System.Object)", "methodShortName": "MapGuestSettings(...)", "fileIndex": 0, "line": 1747,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "CreateM365UserAsync()", "methodShortName": "CreateM365UserAsync()", "fileIndex": 0, "line": 95,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "GetUserLicensesAsync()", "methodShortName": "GetUserLicensesAsync()", "fileIndex": 0, "line": 1334,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.GraphAdminNotificationService", "reportPath": "TeamsManager.Core_GraphAdminNotificationService.html", "methodName": "BuildHtmlMessage(System.String,System.Collections.Generic.Dictionary`2<System.String,System.Object>,System.Collections.Generic.Dictionary`2<System.String,System.Object>,System.Boolean)", "methodShortName": "BuildHtmlMessage(...)", "fileIndex": 0, "line": 280,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.OrganizationalUnit>,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.Department>,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.OrganizationalUnitService>,TeamsManager.Core.Abstractions.Services.Graph.IGraphCacheService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 37,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "GetOrganizationalUnitByIdAsync()", "methodShortName": "GetOrganizationalUnitByIdAsync()", "fileIndex": 0, "line": 56,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolTypeService", "reportPath": "TeamsManager.Core_SchoolTypeService.html", "methodName": "GetSchoolTypeByIdAsync()", "methodShortName": "GetSchoolTypeByIdAsync()", "fileIndex": 0, "line": 59,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SubjectService", "reportPath": "TeamsManager.Core_SubjectService.html", "methodName": "CreateSubjectAsync()", "methodShortName": "CreateSubjectAsync()", "fileIndex": 0, "line": 144,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "RemoveMemberAsync()", "methodShortName": "RemoveMemberAsync()", "fileIndex": 0, "line": 1138,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.BooleanToVisibilityConverter", "reportPath": "TeamsManager.UI_BooleanToVisibilityConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 15,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ConditionalAccessInfo", "reportPath": "TeamsManager.UI_ConditionalAccessInfo.html", "methodName": "ToString()", "methodShortName": "ToString()", "fileIndex": 0, "line": 85,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.ApplicationSettingService", "reportPath": "TeamsManager.UI_ApplicationSettingService.html", "methodName": "GetAllSettingsAsync()", "methodShortName": "GetAllSettingsAsync()", "fileIndex": 0, "line": 38,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.DepartmentCodeMigrationService", "reportPath": "TeamsManager.UI_DepartmentCodeMigrationService.html", "methodName": "MigrateDepartmentCodesAsync()", "methodShortName": "MigrateDepartmentCodesAsync()", "fileIndex": 0, "line": 33,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.SignalRService", "reportPath": "TeamsManager.UI_SignalRService.html", "methodName": "Dispose()", "methodShortName": "Dispose()", "fileIndex": 0, "line": 378,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "SaveAsync()", "methodShortName": "SaveAsync()", "fileIndex": 0, "line": 584,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportProgressViewModel", "reportPath": "TeamsManager.UI_ImportProgressViewModel.html", "methodName": "HandleImportComplete()", "methodShortName": "HandleImportComplete()", "fileIndex": 0, "line": 383,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.MonitoringDashboardViewModel", "reportPath": "TeamsManager.UI_MonitoringDashboardViewModel.html", "methodName": ".ctor(TeamsManager.UI.Services.ISignalRService,TeamsManager.UI.Services.IMonitoringDataService,TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerHealthWidgetViewModel,TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerMetricsWidgetViewModel,TeamsManager.UI.ViewModels.Monitoring.Widgets.ActiveOperationsWidgetViewModel,TeamsManager.UI.ViewModels.Monitoring.Widgets.AlertsWidgetViewModel,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Monitoring.MonitoringDashboardViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 20,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerMetricsWidgetViewModel", "reportPath": "TeamsManager.UI_TeamsManagerMetricsWidgetViewModel.html", "methodName": "RefreshAsync()", "methodShortName": "RefreshAsync()", "fileIndex": 0, "line": 193,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitsManagementViewModel.html", "methodName": "UpdateCommandStates()", "methodShortName": "UpdateCommandStates()", "fileIndex": 0, "line": 499,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingItemViewModel", "reportPath": "TeamsManager.UI_ApplicationSettingItemViewModel.html", "methodName": "Validate()", "methodShortName": "Validate()", "fileIndex": 0, "line": 286,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Shell.MainShellViewModel", "reportPath": "TeamsManager.UI_MainShellViewModel.html", "methodName": "GenerateSecuritySummary(TeamsManager.UI.Models.ConditionalAccessInfo)", "methodShortName": "GenerateSecuritySummary(...)", "fileIndex": 0, "line": 553,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamTemplateEditorViewModel", "reportPath": "TeamsManager.UI_TeamTemplateEditorViewModel.html", "methodName": "SaveTemplateAsync()", "methodShortName": "SaveTemplateAsync()", "fileIndex": 0, "line": 460,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "reportPath": "TeamsManager.UI_UserListViewModel.html", "methodName": "set_CurrentPage(System.Int32)", "methodShortName": "set_CurrentPage(...)", "fileIndex": 0, "line": 184,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel", "reportPath": "TeamsManager.UI_UserSchoolTypeAssignmentViewModel.html", "methodName": "CreateAssignmentModel(TeamsManager.Core.Models.UserSchoolType)", "methodShortName": "CreateAssignmentModel(...)", "fileIndex": 0, "line": 484,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel", "reportPath": "TeamsManager.UI_UserSchoolTypeAssignmentViewModel.html", "methodName": "RemoveAssignmentAsync()", "methodShortName": "RemoveAssignmentAsync()", "fileIndex": 0, "line": 396,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.Dialogs.UniversalDialog", "reportPath": "TeamsManager.UI_UniversalDialog.html", "methodName": "OnKeyDown(System.Windows.Input.KeyEventArgs)", "methodShortName": "OnKeyDown(...)", "fileIndex": 2, "line": 60,
    "metrics": [
      { "value": 210, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MsalAuthService", "reportPath": "TeamsManager.UI_MsalAuthService.html", "methodName": "AcquireTokenInteractiveAsync()", "methodShortName": "AcquireTokenInteractiveAsync()", "fileIndex": 0, "line": 108,
    "metrics": [
      { "value": 196, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.TeamsController", "reportPath": "TeamsManager.Api_TeamsController.html", "methodName": "UpdateTeam()", "methodShortName": "UpdateTeam()", "fileIndex": 0, "line": 170,
    "metrics": [
      { "value": 184, "exceeded": true },
      { "value": 42, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.HealthCheckToPolishConverter", "reportPath": "TeamsManager.UI_HealthCheckToPolishConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 182, "exceeded": true },
      { "value": 13, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "reportPath": "TeamsManager.UI_UniversalDialogViewModel.html", "methodName": "GetPrimaryButtonText()", "methodShortName": "GetPrimaryButtonText()", "fileIndex": 0, "line": 213,
    "metrics": [
      { "value": 182, "exceeded": true },
      { "value": 13, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Swagger.ExampleSchemaFilter", "reportPath": "TeamsManager.Api_ExampleSchemaFilter.html", "methodName": "Apply(Microsoft.OpenApi.Models.OpenApiSchema,Swashbuckle.AspNetCore.SwaggerGen.SchemaFilterContext)", "methodShortName": "Apply(...)", "fileIndex": 0, "line": 15,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "BulkRoleChangeAsync()", "methodShortName": "BulkRoleChangeAsync()", "fileIndex": 0, "line": 315,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "TransferTeamOwnership()", "methodShortName": "TransferTeamOwnership()", "fileIndex": 0, "line": 690,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Common.ModernCircuitBreaker", "reportPath": "TeamsManager.Core_ModernCircuitBreaker.html", "methodName": "OnFailure(System.Exception,System.String)", "methodShortName": "OnFailure(...)", "fileIndex": 0, "line": 115,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphValidationException", "reportPath": "TeamsManager.Core_GraphValidationException.html", "methodName": "GetDetailedErrorMessage()", "methodShortName": "GetDetailedErrorMessage()", "fileIndex": 0, "line": 180,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Helpers.GraphModelMapper", "reportPath": "TeamsManager.Core_GraphModelMapper.html", "methodName": "GetBoolean(System.Object,System.String,System.Boolean)", "methodShortName": "GetBoolean(...)", "fileIndex": 0, "line": 48,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Helpers.GraphModelMapper", "reportPath": "TeamsManager.Core_GraphModelMapper.html", "methodName": "GetNullableInt32(System.Object,System.String)", "methodShortName": "GetNullableInt32(...)", "fileIndex": 0, "line": 110,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Channel", "reportPath": "TeamsManager.Core_Channel.html", "methodName": "get_ShortSummary()", "methodShortName": "get_ShortSummary()", "fileIndex": 0, "line": 257,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Cache.CacheInvalidationService", "reportPath": "TeamsManager.Core_CacheInvalidationService.html", "methodName": "InvalidateForUserUpdatedAsync()", "methodShortName": "InvalidateForUserUpdatedAsync()", "fileIndex": 0, "line": 233,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "GetRateLimitStatusAsync()", "methodShortName": "GetRateLimitStatusAsync()", "fileIndex": 0, "line": 1741,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "RemoveTeamMemberAsync()", "methodShortName": "RemoveTeamMemberAsync()", "fileIndex": 0, "line": 819,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "UpdateTeamMemberRoleAsync()", "methodShortName": "UpdateTeamMemberRoleAsync()", "fileIndex": 0, "line": 889,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "RemoveTeamMemberAsync()", "methodShortName": "RemoveTeamMemberAsync()", "fileIndex": 0, "line": 844,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "UpdateTeamMemberRoleAsync()", "methodShortName": "UpdateTeamMemberRoleAsync()", "fileIndex": 0, "line": 914,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTokenManager", "reportPath": "TeamsManager.Core_GraphTokenManager.html", "methodName": "GetGraphTokenInfoAsync()", "methodShortName": "GetGraphTokenInfoAsync()", "fileIndex": 0, "line": 171,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "MapToLicense(System.Text.Json.JsonElement)", "methodShortName": "MapToLicense(...)", "fileIndex": 0, "line": 1749,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "MapToGraphTeamMember(System.Text.Json.JsonElement)", "methodShortName": "MapToGraphTeamMember(...)", "fileIndex": 0, "line": 1816,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "AddUserToTeamAsync()", "methodShortName": "AddUserToTeamAsync()", "fileIndex": 0, "line": 923,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "FindDuplicateUsersAsync()", "methodShortName": "FindDuplicateUsersAsync()", "fileIndex": 0, "line": 577,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "GetTeamMembersAsync()", "methodShortName": "GetTeamMembersAsync()", "fileIndex": 0, "line": 1075,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "RemoveLicenseFromUserAsync()", "methodShortName": "RemoveLicenseFromUserAsync()", "fileIndex": 0, "line": 1254,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "RemoveUserFromTeamAsync()", "methodShortName": "RemoveUserFromTeamAsync()", "fileIndex": 0, "line": 1003,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "SearchM365UsersAsync()", "methodShortName": "SearchM365UsersAsync()", "fileIndex": 0, "line": 758,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ModernHttpService", "reportPath": "TeamsManager.Core_ModernHttpService.html", "methodName": "GetMailMessagesAsync()", "methodShortName": "GetMailMessagesAsync()", "fileIndex": 0, "line": 576,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.GraphSynchronizerBase<T1, T2>", "reportPath": "TeamsManager.Core_GraphSynchronizerBase_2.html", "methodName": "SynchronizeAsync()", "methodShortName": "SynchronizeAsync()", "fileIndex": 0, "line": 28,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.TeamSynchronizer", "reportPath": "TeamsManager.Core_TeamSynchronizer.html", "methodName": "DetectChangesAsync()", "methodShortName": "DetectChangesAsync()", "fileIndex": 0, "line": 135,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.UserSynchronizer", "reportPath": "TeamsManager.Core_UserSynchronizer.html", "methodName": "DetectChangesAsync()", "methodShortName": "DetectChangesAsync()", "fileIndex": 0, "line": 92,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "InvalidateCache(System.String,System.String,System.Nullable`1<System.Boolean>,System.Nullable`1<System.Boolean>,System.String,System.Nullable`1<System.Boolean>,System.Nullable`1<System.Boolean>,System.Boolean,System.Boolean)", "methodShortName": "InvalidateCache(...)", "fileIndex": 0, "line": 885,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "DeleteUserAsync()", "methodShortName": "DeleteUserAsync()", "fileIndex": 0, "line": 1571,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.ProgressToPercentageConverter", "reportPath": "TeamsManager.UI_ProgressToPercentageConverter.html", "methodName": "Convert(System.Object[],System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 137,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.ProgressToTextConverter", "reportPath": "TeamsManager.UI_ProgressToTextConverter.html", "methodName": "Convert(System.Object[],System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 162,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.StepStatusConverter", "reportPath": "TeamsManager.UI_StepStatusConverter.html", "methodName": "Convert(System.Object[],System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 10,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.StringToTimeConverter", "reportPath": "TeamsManager.UI_StringToTimeConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.WorkloadToColorConverter", "reportPath": "TeamsManager.UI_WorkloadToColorConverter.html", "methodName": "Convert(System.Object[],System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 15,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "ValidateUpn()", "methodShortName": "ValidateUpn()", "fileIndex": 0, "line": 288,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Scripts.TestDepartmentCRUD", "reportPath": "TeamsManager.UI_TestDepartmentCRUD.html", "methodName": "TestDeleteOperationsAsync()", "methodShortName": "TestDeleteOperationsAsync()", "fileIndex": 0, "line": 219,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.ConditionalAccessAnalyzer", "reportPath": "TeamsManager.UI_ConditionalAccessAnalyzer.html", "methodName": "GetSecuritySummary(TeamsManager.UI.Models.ConditionalAccessInfo)", "methodShortName": "GetSecuritySummary(...)", "fileIndex": 0, "line": 232,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Dashboard.SimpleDashboardOperationHistoryService", "reportPath": "TeamsManager.UI_SimpleDashboardOperationHistoryService.html", "methodName": "UpdateOperationStatusAsync(System.String,TeamsManager.Core.Enums.OperationStatus,System.String,System.String)", "methodShortName": "UpdateOperationStatusAsync(...)", "fileIndex": 0, "line": 174,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.GraphUserProfileService", "reportPath": "TeamsManager.UI_GraphUserProfileService.html", "methodName": "TestGraphAccessAsync()", "methodShortName": "TestGraphAccessAsync()", "fileIndex": 0, "line": 111,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MigrationResult", "reportPath": "TeamsManager.UI_MigrationResult.html", "methodName": "GetSummary()", "methodShortName": "GetSummary()", "fileIndex": 0, "line": 235,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dashboard.DashboardViewModel", "reportPath": "TeamsManager.UI_DashboardViewModel.html", "methodName": "LoadRecentOperationsAsync()", "methodShortName": "LoadRecentOperationsAsync()", "fileIndex": 0, "line": 230,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "get_CanSave()", "methodShortName": "get_CanSave()", "fileIndex": 0, "line": 230,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "ApplyWorkingChangesToModel()", "methodShortName": "ApplyWorkingChangesToModel()", "fileIndex": 0, "line": 656,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "ValidateNameConflictAsync()", "methodShortName": "ValidateNameConflictAsync()", "fileIndex": 0, "line": 941,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "ValidateTeamsBeforeDeactivationAsync()", "methodShortName": "ValidateTeamsBeforeDeactivationAsync()", "fileIndex": 0, "line": 1001,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": "FilterDepartments(System.Object)", "methodShortName": "FilterDepartments(...)", "fileIndex": 0, "line": 265,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IDepartmentService,TeamsManager.Core.Abstractions.Services.IOrganizationalUnitService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel>,TeamsManager.UI.Services.Abstractions.IUIDialogService,TeamsManager.UI.ViewModels.Shell.MainShellViewModel,TeamsManager.UI.Services.DepartmentCodeMigrationService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 34,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": "DeleteDepartmentAsync()", "methodShortName": "DeleteDepartmentAsync()", "fileIndex": 0, "line": 426,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": "LoadDepartmentsAsync()", "methodShortName": "LoadDepartmentsAsync()", "fileIndex": 0, "line": 179,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportProgressViewModel", "reportPath": "TeamsManager.UI_ImportProgressViewModel.html", "methodName": "ExecuteImportAsync()", "methodShortName": "ExecuteImportAsync()", "fileIndex": 0, "line": 243,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportValidationViewModel", "reportPath": "TeamsManager.UI_ImportValidationViewModel.html", "methodName": "CanFixError(TeamsManager.Core.Abstractions.Services.ImportValidationError)", "methodShortName": "CanFixError(...)", "fileIndex": 0, "line": 241,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportValidationViewModel", "reportPath": "TeamsManager.UI_ImportValidationViewModel.html", "methodName": "UpdateFilteredItems()", "methodShortName": "UpdateFilteredItems()", "fileIndex": 0, "line": 275,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.LoginViewModel", "reportPath": "TeamsManager.UI_LoginViewModel.html", "methodName": ".ctor(TeamsManager.UI.Services.Abstractions.IMsalAuthService,TeamsManager.UI.Services.Configuration.ConfigurationManager,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.LoginViewModel>,TeamsManager.UI.Services.ConditionalAccessAnalyzer,TeamsManager.Core.Abstractions.ICurrentUserService,TeamsManager.UI.Services.IUserSynchronizationService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 30,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "get_CanSave()", "methodShortName": "get_CanSave()", "fileIndex": 0, "line": 289,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypesListViewModel", "reportPath": "TeamsManager.UI_SchoolTypesListViewModel.html", "methodName": "EditSchoolTypeAsync()", "methodShortName": "EditSchoolTypeAsync()", "fileIndex": 0, "line": 208,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingItemViewModel", "reportPath": "TeamsManager.UI_ApplicationSettingItemViewModel.html", "methodName": "get_DisplayValue()", "methodShortName": "get_DisplayValue()", "fileIndex": 0, "line": 145,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectsViewModel", "reportPath": "TeamsManager.UI_SubjectsViewModel.html", "methodName": "EditSubjectAsync()", "methodShortName": "EditSubjectAsync()", "fileIndex": 0, "line": 297,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.ChannelCardViewModel", "reportPath": "TeamsManager.UI_ChannelCardViewModel.html", "methodName": ".ctor(TeamsManager.Core.Models.Channel,TeamsManager.Core.Abstractions.Services.IChannelService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,TeamsManager.UI.Services.Abstractions.IMsalAuthService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Teams.ChannelCardViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 31,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamChannelsViewModel", "reportPath": "TeamsManager.UI_TeamChannelsViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IChannelService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,TeamsManager.UI.Services.Abstractions.IMsalAuthService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Teams.TeamChannelsViewModel>,Microsoft.Extensions.Logging.ILoggerFactory)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 32,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamChannelsViewModel", "reportPath": "TeamsManager.UI_TeamChannelsViewModel.html", "methodName": "CreateNewChannelAsync()", "methodShortName": "CreateNewChannelAsync()", "fileIndex": 0, "line": 312,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "reportPath": "TeamsManager.UI_TeamCreationWizardViewModel.html", "methodName": "UpdateSummaryWarnings()", "methodShortName": "UpdateSummaryWarnings()", "fileIndex": 0, "line": 474,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "reportPath": "TeamsManager.UI_TeamCreationWizardViewModel.html", "methodName": "LoadInitialData()", "methodShortName": "LoadInitialData()", "fileIndex": 0, "line": 242,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamListViewModel", "reportPath": "TeamsManager.UI_TeamListViewModel.html", "methodName": "DeleteTeamAsync()", "methodShortName": "DeleteTeamAsync()", "fileIndex": 0, "line": 402,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListItemViewModel", "reportPath": "TeamsManager.UI_UserListItemViewModel.html", "methodName": "get_LastLoginText()", "methodShortName": "get_LastLoginText()", "fileIndex": 0, "line": 67,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "reportPath": "TeamsManager.UI_UserListViewModel.html", "methodName": "DeleteUserAsync()", "methodShortName": "DeleteUserAsync()", "fileIndex": 0, "line": 588,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "LoadTestsToCategories()", "methodShortName": "LoadTestsToCategories()", "fileIndex": 1, "line": 362,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "ExecuteAutomaticTest()", "methodShortName": "ExecuteAutomaticTest()", "fileIndex": 1, "line": 602,
    "metrics": [
      { "value": 156, "exceeded": true },
      { "value": 12, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "RemoveUsersFromAllTeams()", "methodShortName": "RemoveUsersFromAllTeams()", "fileIndex": 0, "line": 757,
    "metrics": [
      { "value": 149, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "GetTeamByIdAsync()", "methodShortName": "GetTeamByIdAsync()", "fileIndex": 0, "line": 114,
    "metrics": [
      { "value": 125, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolYearService", "reportPath": "TeamsManager.Core_SchoolYearService.html", "methodName": "UpdateSchoolYearAsync()", "methodShortName": "UpdateSchoolYearAsync()", "fileIndex": 0, "line": 418,
    "metrics": [
      { "value": 118, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringDataService", "reportPath": "TeamsManager.UI_MonitoringDataService.html", "methodName": "GetPerformanceMetricsAsync()", "methodShortName": "GetPerformanceMetricsAsync()", "fileIndex": 0, "line": 81,
    "metrics": [
      { "value": 117, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamsManagerHealthOrchestrator", "reportPath": "TeamsManager.Application_TeamsManagerHealthOrchestrator.html", "methodName": "GenerateTeamsManagerRecommendations(System.Collections.Generic.List`1<TeamsManager.Core.Models.HealthCheckDetail>,TeamsManager.Core.Models.HealthMetrics)", "methodShortName": "GenerateTeamsManagerRecommendations(...)", "fileIndex": 0, "line": 606,
    "metrics": [
      { "value": 114, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "UpdateTeamAsync()", "methodShortName": "UpdateTeamAsync()", "fileIndex": 0, "line": 545,
    "metrics": [
      { "value": 111, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.DataImportOrchestrator", "reportPath": "TeamsManager.Application_DataImportOrchestrator.html", "methodName": "ProcessUserImportBatchesAsync()", "methodShortName": "ProcessUserImportBatchesAsync()", "fileIndex": 0, "line": 173,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamsManagerHealthOrchestrator", "reportPath": "TeamsManager.Application_TeamsManagerHealthOrchestrator.html", "methodName": "AutoRepairCommonIssuesAsync()", "methodShortName": "AutoRepairCommonIssuesAsync()", "fileIndex": 0, "line": 186,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphConnectionException", "reportPath": "TeamsManager.Core_GraphConnectionException.html", "methodName": "GetRecommendedRetryDelay()", "methodShortName": "GetRecommendedRetryDelay()", "fileIndex": 0, "line": 110,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphValidationException", "reportPath": "TeamsManager.Core_GraphValidationException.html", "methodName": "CreateLengthError(System.String,System.Object,System.Nullable`1<System.Int32>,System.Nullable`1<System.Int32>,System.String,System.String,System.String)", "methodShortName": "CreateLengthError(...)", "fileIndex": 0, "line": 299,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphValidationException", "reportPath": "TeamsManager.Core_GraphValidationException.html", "methodName": "CreateRangeError(System.String,System.Object,System.Object,System.Object,System.String,System.String,System.String)", "methodShortName": "CreateRangeError(...)", "fileIndex": 0, "line": 333,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ApplicationSettingService", "reportPath": "TeamsManager.Core_ApplicationSettingService.html", "methodName": "InvalidateSettingCache(System.String,System.String,System.String,System.Boolean)", "methodShortName": "InvalidateSettingCache(...)", "fileIndex": 0, "line": 551,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Cache.CascadeInvalidationStrategy", "reportPath": "TeamsManager.Core_CascadeInvalidationStrategy.html", "methodName": ".ctor()", "methodShortName": ".ctor()", "fileIndex": 0, "line": 511,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "AutoExtendFrequentlyUsedEntries(System.TimeSpan,System.Int32,System.TimeSpan)", "methodShortName": "AutoExtendFrequentlyUsedEntries(...)", "fileIndex": 0, "line": 810,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "CleanupExpiredEntries()", "methodShortName": "CleanupExpiredEntries()", "fileIndex": 0, "line": 840,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "AutoExtendFrequentlyUsedEntries(System.TimeSpan,System.Int32,System.TimeSpan)", "methodShortName": "AutoExtendFrequentlyUsedEntries(...)", "fileIndex": 0, "line": 841,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "CleanupExpiredEntries()", "methodShortName": "CleanupExpiredEntries()", "fileIndex": 0, "line": 871,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphExceptionHandler", "reportPath": "TeamsManager.Core_GraphExceptionHandler.html", "methodName": "HandleStandardExceptionAsync()", "methodShortName": "HandleStandardExceptionAsync()", "fileIndex": 0, "line": 443,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphService", "reportPath": "TeamsManager.Core_GraphService.html", "methodName": "ShouldRetry(System.Exception)", "methodShortName": "ShouldRetry(...)", "fileIndex": 0, "line": 798,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapDiscoverySettings(System.Object)", "methodShortName": "MapDiscoverySettings(...)", "fileIndex": 0, "line": 1784,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "MapDiscoverySettings(System.Object)", "methodShortName": "MapDiscoverySettings(...)", "fileIndex": 0, "line": 1809,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "AssignLicenseToUserAsync()", "methodShortName": "AssignLicenseToUserAsync()", "fileIndex": 0, "line": 1192,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "DeleteM365UserAsync()", "methodShortName": "DeleteM365UserAsync()", "fileIndex": 0, "line": 253,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "GetAllUsersInternalAsync()", "methodShortName": "GetAllUsersInternalAsync()", "fileIndex": 0, "line": 1547,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "GetAvailableLicensesAsync()", "methodShortName": "GetAvailableLicensesAsync()", "fileIndex": 0, "line": 1409,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "UpdateM365UserPrincipalNameAsync()", "methodShortName": "UpdateM365UserPrincipalNameAsync()", "fileIndex": 0, "line": 315,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OperationHistoryService", "reportPath": "TeamsManager.Core_OperationHistoryService.html", "methodName": "GetHistoryByFilterAsync()", "methodShortName": "GetHistoryByFilterAsync()", "fileIndex": 0, "line": 191,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "GetHierarchyPathAsync()", "methodShortName": "GetHierarchyPathAsync()", "fileIndex": 0, "line": 939,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "IsNameUniqueAsync()", "methodShortName": "IsNameUniqueAsync()", "fileIndex": 0, "line": 852,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SubjectService", "reportPath": "TeamsManager.Core_SubjectService.html", "methodName": "DeleteSubjectAsync()", "methodShortName": "DeleteSubjectAsync()", "fileIndex": 0, "line": 414,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SubjectService", "reportPath": "TeamsManager.Core_SubjectService.html", "methodName": "GetTeachersForSubjectAsync()", "methodShortName": "GetTeachersForSubjectAsync()", "fileIndex": 0, "line": 522,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "GetTeamsByDepartmentAsync()", "methodShortName": "GetTeamsByDepartmentAsync()", "fileIndex": 0, "line": 2050,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "GetTeamsByOwnerAsync()", "methodShortName": "GetTeamsByOwnerAsync()", "fileIndex": 0, "line": 267,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "GetTeamsBySchoolYearAsync()", "methodShortName": "GetTeamsBySchoolYearAsync()", "fileIndex": 0, "line": 2012,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.App", "reportPath": "TeamsManager.UI_App.html", "methodName": "OnStartup(System.Windows.StartupEventArgs)", "methodShortName": "OnStartup(...)", "fileIndex": 1, "line": 765,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.ConnectionStateToColorConverter", "reportPath": "TeamsManager.UI_ConnectionStateToColorConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 68,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.NullToVisibilityConverter", "reportPath": "TeamsManager.UI_NullToVisibilityConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 11,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.StringToDateConverter", "reportPath": "TeamsManager.UI_StringToDateConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.TimeSpanToStringConverter", "reportPath": "TeamsManager.UI_TimeSpanToStringConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 123,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.Configuration.ApiConfiguration", "reportPath": "TeamsManager.UI_ApiConfiguration.html", "methodName": "IsComplete()", "methodShortName": "IsComplete()", "fileIndex": 0, "line": 61,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.Configuration.AzureAdConfiguration", "reportPath": "TeamsManager.UI_AzureAdConfiguration.html", "methodName": "IsComplete()", "methodShortName": "IsComplete()", "fileIndex": 0, "line": 75,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.Configuration.MsalConfiguration", "reportPath": "TeamsManager.UI_MsalConfiguration.html", "methodName": "IsValid()", "methodShortName": "IsValid()", "fileIndex": 0, "line": 25,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "ValidateBirthDate()", "methodShortName": "ValidateBirthDate()", "fileIndex": 0, "line": 352,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "ValidatePassword()", "methodShortName": "ValidatePassword()", "fileIndex": 0, "line": 387,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Scripts.TestDepartmentCRUD", "reportPath": "TeamsManager.UI_TestDepartmentCRUD.html", "methodName": "TestReadOperationsAsync()", "methodShortName": "TestReadOperationsAsync()", "fileIndex": 0, "line": 121,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Configuration.ConfigurationValidator", "reportPath": "TeamsManager.UI_ConfigurationValidator.html", "methodName": "ValidateFullConfigurationAsync()", "methodShortName": "ValidateFullConfigurationAsync()", "fileIndex": 0, "line": 24,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Configuration.MsalConfigurationProvider", "reportPath": "TeamsManager.UI_MsalConfigurationProvider.html", "methodName": "TryLoadFromFile(System.String,TeamsManager.UI.Models.Configuration.MsalConfiguration&)", "methodShortName": "TryLoadFromFile(...)", "fileIndex": 0, "line": 83,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Dashboard.SimpleDashboardOperationHistoryService", "reportPath": "TeamsManager.UI_SimpleDashboardOperationHistoryService.html", "methodName": "GetHistoryByFilterAsync(System.Nullable`1<System.DateTime>,System.Nullable`1<System.DateTime>,System.Nullable`1<TeamsManager.Core.Enums.OperationType>,System.Nullable`1<TeamsManager.Core.Enums.OperationStatus>,System.String,System.Int32,System.Int32)", "methodShortName": "GetHistoryByFilterAsync(...)", "fileIndex": 0, "line": 115,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringDataService", "reportPath": "TeamsManager.UI_MonitoringDataService.html", "methodName": "EstimateCompletion(TeamsManager.Core.Models.OperationHistory)", "methodShortName": "EstimateCompletion(...)", "fileIndex": 0, "line": 467,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.SchoolTypeUIService", "reportPath": "TeamsManager.UI_SchoolTypeUIService.html", "methodName": "UpdateSchoolTypeAsync()", "methodShortName": "UpdateSchoolTypeAsync()", "fileIndex": 0, "line": 128,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "set_DepartmentIsActive(System.Boolean)", "methodShortName": "set_DepartmentIsActive(...)", "fileIndex": 0, "line": 401,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IDepartmentService,TeamsManager.Core.Abstractions.Services.IOrganizationalUnitService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel>,TeamsManager.UI.Services.Abstractions.IUIDialogService,TeamsManager.Core.Abstractions.Services.ITeamService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 54,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "reportPath": "TeamsManager.UI_UniversalDialogViewModel.html", "methodName": "GetSecondaryButtonText()", "methodShortName": "GetSecondaryButtonText()", "fileIndex": 0, "line": 230,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportColumnMappingViewModel", "reportPath": "TeamsManager.UI_ImportColumnMappingViewModel.html", "methodName": "LoadTargetFields()", "methodShortName": "LoadTargetFields()", "fileIndex": 0, "line": 163,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportColumnMappingViewModel", "reportPath": "TeamsManager.UI_ImportColumnMappingViewModel.html", "methodName": "UpdateFieldDescriptions(System.String[])", "methodShortName": "UpdateFieldDescriptions(...)", "fileIndex": 0, "line": 217,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportProgressViewModel", "reportPath": "TeamsManager.UI_ImportProgressViewModel.html", "methodName": "CancelImportAsync()", "methodShortName": "CancelImportAsync()", "fileIndex": 0, "line": 135,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.MonitoringDashboardViewModel", "reportPath": "TeamsManager.UI_MonitoringDashboardViewModel.html", "methodName": "SubscribeToSignalRUpdates()", "methodShortName": "SubscribeToSignalRUpdates()", "fileIndex": 0, "line": 134,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerHealthWidgetViewModel", "reportPath": "TeamsManager.UI_TeamsManagerHealthWidgetViewModel.html", "methodName": "TestGraphConnectionAsync()", "methodShortName": "TestGraphConnectionAsync()", "fileIndex": 0, "line": 290,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "LoadAvailableParentUnitsAsync()", "methodShortName": "LoadAvailableParentUnitsAsync()", "fileIndex": 0, "line": 654,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitsManagementViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IOrganizationalUnitService,TeamsManager.Core.Abstractions.Services.IDepartmentService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel>,TeamsManager.UI.Services.Abstractions.IUIDialogService,TeamsManager.UI.ViewModels.Shell.MainShellViewModel)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 31,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypesListViewModel", "reportPath": "TeamsManager.UI_SchoolTypesListViewModel.html", "methodName": "FilterSchoolTypes(System.Object)", "methodShortName": "FilterSchoolTypes(...)", "fileIndex": 0, "line": 306,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.SchoolYears.SchoolYearListViewModel", "reportPath": "TeamsManager.UI_SchoolYearListViewModel.html", "methodName": "SchoolYearFilter(System.Object)", "methodShortName": "SchoolYearFilter(...)", "fileIndex": 0, "line": 369,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingItemViewModel", "reportPath": "TeamsManager.UI_ApplicationSettingItemViewModel.html", "methodName": "IncrementValue()", "methodShortName": "IncrementValue()", "fileIndex": 0, "line": 230,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingItemViewModel", "reportPath": "TeamsManager.UI_ApplicationSettingItemViewModel.html", "methodName": "DecrementValue()", "methodShortName": "DecrementValue()", "fileIndex": 0, "line": 258,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectImportViewModel", "reportPath": "TeamsManager.UI_SubjectImportViewModel.html", "methodName": "LoadCsvHeaders()", "methodShortName": "LoadCsvHeaders()", "fileIndex": 0, "line": 241,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectImportViewModel", "reportPath": "TeamsManager.UI_SubjectImportViewModel.html", "methodName": "GeneratePreviewAsync()", "methodShortName": "GeneratePreviewAsync()", "fileIndex": 0, "line": 339,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectsViewModel", "reportPath": "TeamsManager.UI_SubjectsViewModel.html", "methodName": "AddSubjectAsync()", "methodShortName": "AddSubjectAsync()", "fileIndex": 0, "line": 249,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamChannelsViewModel", "reportPath": "TeamsManager.UI_TeamChannelsViewModel.html", "methodName": "FilterChannels(System.Object)", "methodShortName": "FilterChannels(...)", "fileIndex": 0, "line": 216,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamLifecycleDialogViewModel", "reportPath": "TeamsManager.UI_TeamLifecycleDialogViewModel.html", "methodName": "get_CanExecuteOperation()", "methodShortName": "get_CanExecuteOperation()", "fileIndex": 0, "line": 389,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamLifecycleDialogViewModel", "reportPath": "TeamsManager.UI_TeamLifecycleDialogViewModel.html", "methodName": "SimulateProgressAsync()", "methodShortName": "SimulateProgressAsync()", "fileIndex": 0, "line": 593,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamTemplateEditorViewModel", "reportPath": "TeamsManager.UI_TeamTemplateEditorViewModel.html", "methodName": "InsertToken(TeamsManager.UI.ViewModels.Teams.PlaceholderToken)", "methodShortName": "InsertToken(...)", "fileIndex": 0, "line": 533,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserDetailViewModel", "reportPath": "TeamsManager.UI_UserDetailViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IUserService,TeamsManager.Core.Abstractions.Services.IDepartmentService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Users.UserDetailViewModel>,TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel,TeamsManager.UI.Services.Abstractions.IMsalAuthService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 38,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserDetailViewModel", "reportPath": "TeamsManager.UI_UserDetailViewModel.html", "methodName": "SaveAsync()", "methodShortName": "SaveAsync()", "fileIndex": 0, "line": 310,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "reportPath": "TeamsManager.UI_UserListViewModel.html", "methodName": "OnUserSelectionChanged(System.Object,System.ComponentModel.PropertyChangedEventArgs)", "methodShortName": "OnUserSelectionChanged(...)", "fileIndex": 0, "line": 744,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "reportPath": "TeamsManager.UI_UserListViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IUserService,TeamsManager.Core.Abstractions.Services.IDepartmentService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Users.UserListViewModel>,TeamsManager.UI.ViewModels.Shell.MainShellViewModel,TeamsManager.UI.Services.Abstractions.IMsalAuthService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 37,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel", "reportPath": "TeamsManager.UI_UserSchoolTypeAssignmentViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IUserService,TeamsManager.Core.Abstractions.Services.ISchoolTypeService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 30,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel", "reportPath": "TeamsManager.UI_UserSchoolTypeAssignmentViewModel.html", "methodName": "AddAssignmentAsync()", "methodShortName": "AddAssignmentAsync()", "fileIndex": 0, "line": 263,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel", "reportPath": "TeamsManager.UI_UserSchoolTypeAssignmentViewModel.html", "methodName": "LoadAssignmentsAsync()", "methodShortName": "LoadAssignmentsAsync()", "fileIndex": 0, "line": 174,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel", "reportPath": "TeamsManager.UI_UserSchoolTypeAssignmentViewModel.html", "methodName": "LoadAvailableSchoolTypesAsync()", "methodShortName": "LoadAvailableSchoolTypesAsync()", "fileIndex": 0, "line": 228,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "AddAutomaticTestButton(TeamsManager.UI.Models.TestCase)", "methodShortName": "AddAutomaticTestButton(...)", "fileIndex": 1, "line": 544,
    "metrics": [
      { "value": 110, "exceeded": true },
      { "value": 10, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "DeactivateUserAsync()", "methodShortName": "DeactivateUserAsync()", "fileIndex": 0, "line": 1290,
    "metrics": [
      { "value": 109, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Validation.OrganizationalUnitValidator", "reportPath": "TeamsManager.Core_OrganizationalUnitValidator.html", "methodName": "ValidateOrganizationSpecificRulesAsync()", "methodShortName": "ValidateOrganizationSpecificRulesAsync()", "fileIndex": 0, "line": 335,
    "metrics": [
      { "value": 109, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.DepartmentService", "reportPath": "TeamsManager.Core_DepartmentService.html", "methodName": "DeleteDepartmentAsync()", "methodShortName": "DeleteDepartmentAsync()", "fileIndex": 0, "line": 464,
    "metrics": [
      { "value": 102, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphExceptionHandler", "reportPath": "TeamsManager.Core_GraphExceptionHandler.html", "methodName": "HandleGraphConnectionExceptionAsync()", "methodShortName": "HandleGraphConnectionExceptionAsync()", "fileIndex": 0, "line": 502,
    "metrics": [
      { "value": 102, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "BulkArchiveTeamsWithCleanupAsync()", "methodShortName": "BulkArchiveTeamsWithCleanupAsync()", "fileIndex": 0, "line": 77,
    "metrics": [
      { "value": 93, "exceeded": true },
      { "value": 46, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "ValidateCache(System.String,System.String)", "methodShortName": "ValidateCache(...)", "fileIndex": 0, "line": 1162,
    "metrics": [
      { "value": 93, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "AssignTeacherToSubjectAsync()", "methodShortName": "AssignTeacherToSubjectAsync()", "fileIndex": 0, "line": 955,
    "metrics": [
      { "value": 90, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.AlertLevelToColorConverter", "reportPath": "TeamsManager.UI_AlertLevelToColorConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 41,
    "metrics": [
      { "value": 90, "exceeded": true },
      { "value": 9, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "reportPath": "TeamsManager.UI_UniversalDialogViewModel.html", "methodName": "GetIconKind()", "methodShortName": "GetIconKind()", "fileIndex": 0, "line": 243,
    "metrics": [
      { "value": 90, "exceeded": true },
      { "value": 9, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "reportPath": "TeamsManager.UI_UniversalDialogViewModel.html", "methodName": "GetIconColor()", "methodShortName": "GetIconColor()", "fileIndex": 0, "line": 257,
    "metrics": [
      { "value": 90, "exceeded": true },
      { "value": 9, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "reportPath": "TeamsManager.UI_UniversalDialogViewModel.html", "methodName": "GetIconBackground()", "methodShortName": "GetIconBackground()", "fileIndex": 0, "line": 271,
    "metrics": [
      { "value": 90, "exceeded": true },
      { "value": 9, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkRemoveUsersFromTeamAsync()", "methodShortName": "BulkRemoveUsersFromTeamAsync()", "fileIndex": 0, "line": 171,
    "metrics": [
      { "value": 83, "exceeded": true },
      { "value": 58, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ChannelService", "reportPath": "TeamsManager.Core_ChannelService.html", "methodName": "RemoveTeamChannelAsync()", "methodShortName": "RemoveTeamChannelAsync()", "fileIndex": 0, "line": 566,
    "metrics": [
      { "value": 80, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "CreateUserAsync()", "methodShortName": "CreateUserAsync()", "fileIndex": 0, "line": 276,
    "metrics": [
      { "value": 78, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ChannelService", "reportPath": "TeamsManager.Core_ChannelService.html", "methodName": "MapGraphChannelToLocalChannel(TeamsManager.Core.Models.Graph.GraphChannel,System.String)", "methodShortName": "MapGraphChannelToLocalChannel(...)", "fileIndex": 0, "line": 62,
    "metrics": [
      { "value": 74, "exceeded": true },
      { "value": 54, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ChannelService", "reportPath": "TeamsManager.Core_ChannelService.html", "methodName": "UpdateTeamChannelAsync()", "methodShortName": "UpdateTeamChannelAsync()", "fileIndex": 0, "line": 430,
    "metrics": [
      { "value": 74, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.DiagnosticsController", "reportPath": "TeamsManager.Api_DiagnosticsController.html", "methodName": "DiagnoseConnectionExtendedAsync()", "methodShortName": "DiagnoseConnectionExtendedAsync()", "fileIndex": 0, "line": 67,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.HealthChecks.GraphConnectionHealthCheck", "reportPath": "TeamsManager.Api_GraphConnectionHealthCheck.html", "methodName": "CheckHealthAsync()", "methodShortName": "CheckHealthAsync()", "fileIndex": 0, "line": 26,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Swagger.ExampleSchemaFilter", "reportPath": "TeamsManager.Api_ExampleSchemaFilter.html", "methodName": "AddDtoExamples(Microsoft.OpenApi.Models.OpenApiSchema,Swashbuckle.AspNetCore.SwaggerGen.SchemaFilterContext)", "methodShortName": "AddDtoExamples(...)", "fileIndex": 0, "line": 100,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "BulkAddUsersToTeamsWithGraphApiAsync()", "methodShortName": "BulkAddUsersToTeamsWithGraphApiAsync()", "fileIndex": 0, "line": 1038,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "BulkDeactivateUsersWithGraphApiAsync()", "methodShortName": "BulkDeactivateUsersWithGraphApiAsync()", "fileIndex": 0, "line": 1181,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "BulkRemoveUsersFromTeamsWithGraphApiAsync()", "methodShortName": "BulkRemoveUsersFromTeamsWithGraphApiAsync()", "fileIndex": 0, "line": 1110,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.DataImportOrchestrator", "reportPath": "TeamsManager.Application_DataImportOrchestrator.html", "methodName": "ParseCsvUsersAsync()", "methodShortName": "ParseCsvUsersAsync()", "fileIndex": 0, "line": 117,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "reportPath": "TeamsManager.Core_GraphApiException.html", "methodName": "get_IsPermissionError()", "methodShortName": "get_IsPermissionError()", "fileIndex": 0, "line": 65,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "reportPath": "TeamsManager.Core_GraphApiException.html", "methodName": "get_IsAuthenticationError()", "methodShortName": "get_IsAuthenticationError()", "fileIndex": 0, "line": 72,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "reportPath": "TeamsManager.Core_GraphApiException.html", "methodName": "get_IsValidationError()", "methodShortName": "get_IsValidationError()", "fileIndex": 0, "line": 85,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "reportPath": "TeamsManager.Core_GraphApiException.html", "methodName": "get_IsNotFoundError()", "methodShortName": "get_IsNotFoundError()", "fileIndex": 0, "line": 92,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "reportPath": "TeamsManager.Core_GraphApiException.html", "methodName": "get_IsConflictError()", "methodShortName": "get_IsConflictError()", "fileIndex": 0, "line": 99,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "reportPath": "TeamsManager.Core_GraphApiException.html", "methodName": "GetRecommendedRetryDelay()", "methodShortName": "GetRecommendedRetryDelay()", "fileIndex": 0, "line": 165,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphRateLimitException", "reportPath": "TeamsManager.Core_GraphRateLimitException.html", "methodName": "CreateFromHeaders(System.Int32,System.Nullable`1<System.Int32>,System.Nullable`1<System.Int32>,System.Nullable`1<System.Int32>,System.Nullable`1<System.Int32>,System.String,System.String)", "methodShortName": "CreateFromHeaders(...)", "fileIndex": 0, "line": 362,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Extensions.EnumExtensions", "reportPath": "TeamsManager.Core_EnumExtensions.html", "methodName": "ToPolishString(TeamsManager.Core.Enums.UserRole)", "methodShortName": "ToPolishString(...)", "fileIndex": 0, "line": 105,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Extensions.EnumExtensions", "reportPath": "TeamsManager.Core_EnumExtensions.html", "methodName": "ToPolishString(TeamsManager.Core.Enums.UserRole)", "methodShortName": "ToPolishString(...)", "fileIndex": 0, "line": 104,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Helpers.GraphModelMapper", "reportPath": "TeamsManager.Core_GraphModelMapper.html", "methodName": "GetString(System.Object,System.String,System.String)", "methodShortName": "GetString(...)", "fileIndex": 0, "line": 20,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Channel", "reportPath": "TeamsManager.Core_Channel.html", "methodName": "get_ActivityLevel()", "methodShortName": "get_ActivityLevel()", "fileIndex": 0, "line": 239,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Channel", "reportPath": "TeamsManager.Core_Channel.html", "methodName": "UpdateActivityStats(System.Nullable`1<System.Int32>,System.Nullable`1<System.Int32>,System.Nullable`1<System.Int64>,System.String)", "methodShortName": "UpdateActivityStats(...)", "fileIndex": 0, "line": 334,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.License", "reportPath": "TeamsManager.Core_License.html", "methodName": "ToString()", "methodShortName": "ToString()", "fileIndex": 0, "line": 56,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.TeamMember", "reportPath": "TeamsManager.Core_TeamMember.html", "methodName": "WasActiveOnDate(System.DateTime)", "methodShortName": "WasActiveOnDate(...)", "fileIndex": 0, "line": 358,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.TeamTemplate", "reportPath": "TeamsManager.Core_TeamTemplate.html", "methodName": "get_PopularityLevel()", "methodShortName": "get_PopularityLevel()", "fileIndex": 0, "line": 172,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ApplicationSettingService", "reportPath": "TeamsManager.Core_ApplicationSettingService.html", "methodName": "GetSettingByKeyAsync()", "methodShortName": "GetSettingByKeyAsync()", "fileIndex": 0, "line": 60,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ApplicationSettingService", "reportPath": "TeamsManager.Core_ApplicationSettingService.html", "methodName": "GetSettingsByCategoryAsync()", "methodShortName": "GetSettingsByCategoryAsync()", "fileIndex": 0, "line": 169,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ChannelService", "reportPath": "TeamsManager.Core_ChannelService.html", "methodName": "GetTeamChannelByIdAsync()", "methodShortName": "GetTeamChannelByIdAsync()", "fileIndex": 0, "line": 225,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "GetTeamGroupCacheStats()", "methodShortName": "GetTeamGroupCacheStats()", "fileIndex": 0, "line": 612,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "GetRemainingTtl(System.String)", "methodShortName": "GetRemainingTtl(...)", "fileIndex": 0, "line": 683,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "WarmCacheAsync()", "methodShortName": "WarmCacheAsync()", "fileIndex": 0, "line": 1032,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphExceptionHandler", "reportPath": "TeamsManager.Core_GraphExceptionHandler.html", "methodName": "HandleGraphConnectionExceptionAsync()", "methodShortName": "HandleGraphConnectionExceptionAsync()", "fileIndex": 0, "line": 550,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamMemberAsync()", "methodShortName": "GetTeamMemberAsync()", "fileIndex": 0, "line": 677,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "RemoveTeamChannelAsync()", "methodShortName": "RemoveTeamChannelAsync()", "fileIndex": 0, "line": 1301,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "GetTeamMemberAsync()", "methodShortName": "GetTeamMemberAsync()", "fileIndex": 0, "line": 702,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "RemoveTeamChannelAsync()", "methodShortName": "RemoveTeamChannelAsync()", "fileIndex": 0, "line": 1326,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTokenManager", "reportPath": "TeamsManager.Core_GraphTokenManager.html", "methodName": "GetValidGraphTokenAsync()", "methodShortName": "GetValidGraphTokenAsync()", "fileIndex": 0, "line": 43,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "GetUserByUpnAsync()", "methodShortName": "GetUserByUpnAsync()", "fileIndex": 0, "line": 1633,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "RevokeUserSignInSessionsAsync()", "methodShortName": "RevokeUserSignInSessionsAsync()", "fileIndex": 0, "line": 859,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ModernHttpService", "reportPath": "TeamsManager.Core_ModernHttpService.html", "methodName": "SendMailOnBehalfOfUserAsync()", "methodShortName": "SendMailOnBehalfOfUserAsync()", "fileIndex": 0, "line": 441,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OperationHistoryService", "reportPath": "TeamsManager.Core_OperationHistoryService.html", "methodName": "UpdateOperationProgressAsync()", "methodShortName": "UpdateOperationProgressAsync()", "fileIndex": 0, "line": 135,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "GetAllOrganizationalUnitsAsync()", "methodShortName": "GetAllOrganizationalUnitsAsync()", "fileIndex": 0, "line": 111,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolTypeService", "reportPath": "TeamsManager.Core_SchoolTypeService.html", "methodName": "CreateSchoolTypeAsync()", "methodShortName": "CreateSchoolTypeAsync()", "fileIndex": 0, "line": 128,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SubjectService", "reportPath": "TeamsManager.Core_SubjectService.html", "methodName": "InvalidateCache(System.String,System.Boolean,System.Boolean)", "methodShortName": "InvalidateCache(...)", "fileIndex": 0, "line": 591,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SubjectService", "reportPath": "TeamsManager.Core_SubjectService.html", "methodName": "GetSubjectByIdAsync()", "methodShortName": "GetSubjectByIdAsync()", "fileIndex": 0, "line": 76,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "<UpdateTeamAsync()", "methodShortName": "<UpdateTeamAsync()", "fileIndex": 0, "line": 679,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "GetActiveTeamsAsync()", "methodShortName": "GetActiveTeamsAsync()", "fileIndex": 0, "line": 218,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "GetArchivedTeamsAsync()", "methodShortName": "GetArchivedTeamsAsync()", "fileIndex": 0, "line": 243,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "RestoreTeamAsync()", "methodShortName": "RestoreTeamAsync()", "fileIndex": 0, "line": 855,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "CloneTemplateAsync()", "methodShortName": "CloneTemplateAsync()", "fileIndex": 0, "line": 732,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "GetAllActiveTemplatesAsync()", "methodShortName": "GetAllActiveTemplatesAsync()", "fileIndex": 0, "line": 121,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "GetTemplatesBySchoolTypeAsync()", "methodShortName": "GetTemplatesBySchoolTypeAsync()", "fileIndex": 0, "line": 166,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.App", "reportPath": "TeamsManager.UI_App.html", "methodName": "GetDatabaseConnectionString(Microsoft.Extensions.Configuration.IConfiguration)", "methodShortName": "GetDatabaseConnectionString(...)", "fileIndex": 1, "line": 666,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.EnumDescriptionConverter", "reportPath": "TeamsManager.UI_EnumDescriptionConverter.html", "methodName": "ConvertCamelCaseToSpaces(System.String)", "methodShortName": "ConvertCamelCaseToSpaces(...)", "fileIndex": 0, "line": 72,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.HealthCheckToColorConverter", "reportPath": "TeamsManager.UI_HealthCheckToColorConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 15,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.PercentageToColorConverter", "reportPath": "TeamsManager.UI_PercentageToColorConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 98,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.StringToTimeConverter", "reportPath": "TeamsManager.UI_StringToTimeConverter.html", "methodName": "ConvertBack(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "ConvertBack(...)", "fileIndex": 0, "line": 52,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.TeamMemberRoleToStringConverter", "reportPath": "TeamsManager.UI_TeamMemberRoleToStringConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 15,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.TeamStatusToColorConverter", "reportPath": "TeamsManager.UI_TeamStatusToColorConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 12,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.UserRoleToPolishConverter", "reportPath": "TeamsManager.UI_UserRoleToPolishConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 15,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.WorkloadToColorSingleConverter", "reportPath": "TeamsManager.UI_WorkloadToColorSingleConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 41,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "get_StatusColor()", "methodShortName": "get_StatusColor()", "fileIndex": 0, "line": 194,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "get_RolePermissionsDescription()", "methodShortName": "get_RolePermissionsDescription()", "fileIndex": 0, "line": 226,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "ValidateEmploymentDate()", "methodShortName": "ValidateEmploymentDate()", "fileIndex": 0, "line": 373,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Scripts.TestDepartmentCRUD", "reportPath": "TeamsManager.UI_TestDepartmentCRUD.html", "methodName": "TestCreateOperationsAsync()", "methodShortName": "TestCreateOperationsAsync()", "fileIndex": 0, "line": 58,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.ApplicationSettingService", "reportPath": "TeamsManager.UI_ApplicationSettingService.html", "methodName": "GetSettingsByCategoryAsync()", "methodShortName": "GetSettingsByCategoryAsync()", "fileIndex": 0, "line": 119,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.ConditionalAccessAnalyzer", "reportPath": "TeamsManager.UI_ConditionalAccessAnalyzer.html", "methodName": "GetAuthenticationMethods(System.IdentityModel.Tokens.Jwt.JwtSecurityToken)", "methodShortName": "GetAuthenticationMethods(...)", "fileIndex": 0, "line": 117,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.ConditionalAccessAnalyzer", "reportPath": "TeamsManager.UI_ConditionalAccessAnalyzer.html", "methodName": "ParseMfaTime(System.IdentityModel.Tokens.Jwt.JwtSecurityToken)", "methodShortName": "ParseMfaTime(...)", "fileIndex": 0, "line": 149,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Configuration.ConfigurationManager", "reportPath": "TeamsManager.UI_ConfigurationManager.html", "methodName": "LoadLoginSettingsAsync()", "methodShortName": "LoadLoginSettingsAsync()", "fileIndex": 0, "line": 188,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Configuration.ConfigurationManager", "reportPath": "TeamsManager.UI_ConfigurationManager.html", "methodName": "LoadOAuthConfigAsync()", "methodShortName": "LoadOAuthConfigAsync()", "fileIndex": 0, "line": 58,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Configuration.ConfigurationValidator", "reportPath": "TeamsManager.UI_ConfigurationValidator.html", "methodName": "IsValidApiScope(System.String)", "methodShortName": "IsValidApiScope(...)", "fileIndex": 0, "line": 287,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.DepartmentCodeMigrationService", "reportPath": "TeamsManager.UI_DepartmentCodeMigrationService.html", "methodName": "GenerateDepartmentCodeAsync()", "methodShortName": "GenerateDepartmentCodeAsync()", "fileIndex": 0, "line": 157,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringPerformanceOptimizer", "reportPath": "TeamsManager.UI_MonitoringPerformanceOptimizer.html", "methodName": "IsHealthDataSignificantlyDifferent(TeamsManager.UI.Models.Monitoring.SystemHealthData,TeamsManager.UI.Models.Monitoring.SystemHealthData)", "methodShortName": "IsHealthDataSignificantlyDifferent(...)", "fileIndex": 0, "line": 335,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.DepartmentTreeService", "reportPath": "TeamsManager.UI_DepartmentTreeService.html", "methodName": "BuildDepartmentTree(System.Collections.Generic.IEnumerable`1<TeamsManager.Core.Models.Department>)", "methodShortName": "BuildDepartmentTree(...)", "fileIndex": 0, "line": 18,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.SchoolTypeUIService", "reportPath": "TeamsManager.UI_SchoolTypeUIService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.ISchoolTypeService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.Services.UI.SchoolTypeUIService>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 25,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.SchoolYearUIService", "reportPath": "TeamsManager.UI_SchoolYearUIService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.ISchoolYearService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.Services.UI.SchoolYearUIService>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 25,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.SchoolYearUIService", "reportPath": "TeamsManager.UI_SchoolYearUIService.html", "methodName": "CreateSchoolYearAsync()", "methodShortName": "CreateSchoolYearAsync()", "fileIndex": 0, "line": 162,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.SchoolYearUIService", "reportPath": "TeamsManager.UI_SchoolYearUIService.html", "methodName": "DeleteSchoolYearAsync()", "methodShortName": "DeleteSchoolYearAsync()", "fileIndex": 0, "line": 262,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.SchoolYearUIService", "reportPath": "TeamsManager.UI_SchoolYearUIService.html", "methodName": "SetCurrentSchoolYearAsync()", "methodShortName": "SetCurrentSchoolYearAsync()", "fileIndex": 0, "line": 106,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.SchoolYearUIService", "reportPath": "TeamsManager.UI_SchoolYearUIService.html", "methodName": "UpdateSchoolYearAsync()", "methodShortName": "UpdateSchoolYearAsync()", "fileIndex": 0, "line": 213,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UserSynchronizationService", "reportPath": "TeamsManager.UI_UserSynchronizationService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IUserService,TeamsManager.UI.Services.Abstractions.IGraphUserProfileService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.Services.UserSynchronizationService>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 34,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.UserControls.Import.ColumnMappingStep", "reportPath": "TeamsManager.UI_ColumnMappingStep.html", "methodName": "GeneratePreviewColumns(TeamsManager.UI.ViewModels.Import.ImportColumnMappingViewModel)", "methodShortName": "GeneratePreviewColumns(...)", "fileIndex": 1, "line": 26,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.AsyncRelayCommand<T>", "reportPath": "TeamsManager.UI_AsyncRelayCommand_1.html", "methodName": "CanExecute(System.Object)", "methodShortName": "CanExecute(...)", "fileIndex": 0, "line": 194,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dashboard.DashboardViewModel", "reportPath": "TeamsManager.UI_DashboardViewModel.html", "methodName": "LoadNotifications()", "methodShortName": "LoadNotifications()", "fileIndex": 0, "line": 343,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "get_WindowTitle()", "methodShortName": "get_WindowTitle()", "fileIndex": 0, "line": 187,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "get_ShowHierarchyInfo()", "methodShortName": "get_ShowHierarchyInfo()", "fileIndex": 0, "line": 225,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "Cancel()", "methodShortName": "Cancel()", "fileIndex": 0, "line": 687,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": "HasMatchingChild(TeamsManager.UI.ViewModels.Departments.DepartmentTreeItemViewModel,System.String)", "methodShortName": "HasMatchingChild(...)", "fileIndex": 0, "line": 277,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": "AddDepartmentAsync()", "methodShortName": "AddDepartmentAsync()", "fileIndex": 0, "line": 306,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.BulkImportWizardViewModel", "reportPath": "TeamsManager.UI_BulkImportWizardViewModel.html", "methodName": "CancelAsync()", "methodShortName": "CancelAsync()", "fileIndex": 0, "line": 338,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.BulkImportWizardViewModel", "reportPath": "TeamsManager.UI_BulkImportWizardViewModel.html", "methodName": "StartImportAsync()", "methodShortName": "StartImportAsync()", "fileIndex": 0, "line": 300,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportColumnMappingViewModel", "reportPath": "TeamsManager.UI_ImportColumnMappingViewModel.html", "methodName": "UpdateMappingValidation()", "methodShortName": "UpdateMappingValidation()", "fileIndex": 0, "line": 393,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportFileSelectionViewModel", "reportPath": "TeamsManager.UI_ImportFileSelectionViewModel.html", "methodName": "LoadFileAsync()", "methodShortName": "LoadFileAsync()", "fileIndex": 0, "line": 205,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportProgressViewModel", "reportPath": "TeamsManager.UI_ImportProgressViewModel.html", "methodName": "Dispose()", "methodShortName": "Dispose()", "fileIndex": 0, "line": 500,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportValidationViewModel", "reportPath": "TeamsManager.UI_ImportValidationViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IDataImportOrchestrator,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Import.ImportValidationViewModel>,TeamsManager.UI.Services.Abstractions.IUIDialogService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 25,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportValidationViewModel", "reportPath": "TeamsManager.UI_ImportValidationViewModel.html", "methodName": "AutoFixErrorsAsync()", "methodShortName": "AutoFixErrorsAsync()", "fileIndex": 0, "line": 308,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerMetricsWidgetViewModel", "reportPath": "TeamsManager.UI_TeamsManagerMetricsWidgetViewModel.html", "methodName": "ShowNotification(System.String,System.String,System.String)", "methodShortName": "ShowNotification(...)", "fileIndex": 0, "line": 349,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryItemViewModel", "reportPath": "TeamsManager.UI_OperationHistoryItemViewModel.html", "methodName": "get_FormattedDuration()", "methodShortName": "get_FormattedDuration()", "fileIndex": 0, "line": 58,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryItemViewModel", "reportPath": "TeamsManager.UI_OperationHistoryItemViewModel.html", "methodName": "get_DisplayTarget()", "methodShortName": "get_DisplayTarget()", "fileIndex": 0, "line": 149,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryItemViewModel", "reportPath": "TeamsManager.UI_OperationHistoryItemViewModel.html", "methodName": "GetEntityDisplayName()", "methodShortName": "GetEntityDisplayName()", "fileIndex": 0, "line": 205,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryItemViewModel", "reportPath": "TeamsManager.UI_OperationHistoryItemViewModel.html", "methodName": "get_RelativeStartTime()", "methodShortName": "get_RelativeStartTime()", "fileIndex": 0, "line": 249,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryViewModel", "reportPath": "TeamsManager.UI_OperationHistoryViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.Services.IUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Operations.OperationHistoryViewModel>,TeamsManager.UI.Services.Abstractions.IUIDialogService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 57,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryViewModel", "reportPath": "TeamsManager.UI_OperationHistoryViewModel.html", "methodName": "GetUserDisplayNamesAsync()", "methodShortName": "GetUserDisplayNamesAsync()", "fileIndex": 0, "line": 874,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "get_WindowTitle()", "methodShortName": "get_WindowTitle()", "fileIndex": 0, "line": 247,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "get_ShowHierarchyInfo()", "methodShortName": "get_ShowHierarchyInfo()", "fileIndex": 0, "line": 284,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "get_ParentUnitId()", "methodShortName": "get_ParentUnitId()", "fileIndex": 0, "line": 430,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "set_ParentUnitId(System.String)", "methodShortName": "set_ParentUnitId(...)", "fileIndex": 0, "line": 438,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "NormalizeUnitName(System.String)", "methodShortName": "NormalizeUnitName(...)", "fileIndex": 0, "line": 1107,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IOrganizationalUnitService,TeamsManager.Core.Abstractions.Services.IDepartmentService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel>,TeamsManager.UI.Services.Abstractions.IUIDialogService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 86,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "Cancel()", "methodShortName": "Cancel()", "fileIndex": 0, "line": 945,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "LoadAssignedDepartmentsAsync()", "methodShortName": "LoadAssignedDepartmentsAsync()", "fileIndex": 0, "line": 788,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitsManagementViewModel.html", "methodName": "ContainsSearchText(TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitTreeItemViewModel,System.String)", "methodShortName": "ContainsSearchText(...)", "fileIndex": 0, "line": 258,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitsManagementViewModel.html", "methodName": "AddOrganizationalUnitAsync()", "methodShortName": "AddOrganizationalUnitAsync()", "fileIndex": 0, "line": 276,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitsManagementViewModel.html", "methodName": "DeleteOrganizationalUnitAsync()", "methodShortName": "DeleteOrganizationalUnitAsync()", "fileIndex": 0, "line": 404,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypesListViewModel", "reportPath": "TeamsManager.UI_SchoolTypesListViewModel.html", "methodName": "AddNewSchoolTypeAsync()", "methodShortName": "AddNewSchoolTypeAsync()", "fileIndex": 0, "line": 170,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Shell.MainShellViewModel", "reportPath": "TeamsManager.UI_MainShellViewModel.html", "methodName": "get_UserInitials()", "methodShortName": "get_UserInitials()", "fileIndex": 0, "line": 191,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectEditViewModel", "reportPath": "TeamsManager.UI_SubjectEditViewModel.html", "methodName": "get_IsValid()", "methodShortName": "get_IsValid()", "fileIndex": 0, "line": 144,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectImportViewModel", "reportPath": "TeamsManager.UI_SubjectImportViewModel.html", "methodName": "CanGoNext()", "methodShortName": "CanGoNext()", "fileIndex": 0, "line": 290,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectsViewModel", "reportPath": "TeamsManager.UI_SubjectsViewModel.html", "methodName": "DeleteSubjectAsync()", "methodShortName": "DeleteSubjectAsync()", "fileIndex": 0, "line": 349,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "reportPath": "TeamsManager.UI_TeamCreationWizardViewModel.html", "methodName": "LoadTemplatesForSchoolType()", "methodShortName": "LoadTemplatesForSchoolType()", "fileIndex": 0, "line": 305,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamLifecycleDialogViewModel", "reportPath": "TeamsManager.UI_TeamLifecycleDialogViewModel.html", "methodName": "ShowResultsAsync()", "methodShortName": "ShowResultsAsync()", "fileIndex": 0, "line": 612,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamMembersViewModel", "reportPath": "TeamsManager.UI_TeamMembersViewModel.html", "methodName": "LoadDataAsync()", "methodShortName": "LoadDataAsync()", "fileIndex": 0, "line": 146,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamTemplateEditorViewModel", "reportPath": "TeamsManager.UI_TeamTemplateEditorViewModel.html", "methodName": "get_CanSave()", "methodShortName": "get_CanSave()", "fileIndex": 0, "line": 156,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListItemViewModel", "reportPath": "TeamsManager.UI_UserListItemViewModel.html", "methodName": "get_RoleIcon()", "methodShortName": "get_RoleIcon()", "fileIndex": 0, "line": 87,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "reportPath": "TeamsManager.UI_UserListViewModel.html", "methodName": "UpdatePagination()", "methodShortName": "UpdatePagination()", "fileIndex": 0, "line": 423,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "reportPath": "TeamsManager.UI_UserListViewModel.html", "methodName": "DeleteSelectedAsync()", "methodShortName": "DeleteSelectedAsync()", "fileIndex": 0, "line": 531,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "reportPath": "TeamsManager.UI_UserListViewModel.html", "methodName": "ViewUserDetails()", "methodShortName": "ViewUserDetails()", "fileIndex": 0, "line": 650,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.Import.BulkImportWizardWindow", "reportPath": "TeamsManager.UI_BulkImportWizardWindow.html", "methodName": "BulkImportWizardWindow_Closing(System.Object,System.ComponentModel.CancelEventArgs)", "methodShortName": "BulkImportWizardWindow_Closing(...)", "fileIndex": 1, "line": 36,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "TestCategoryList_SelectionChanged(System.Object,System.Windows.Controls.SelectionChangedEventArgs)", "methodShortName": "TestCategoryList_SelectionChanged(...)", "fileIndex": 1, "line": 465,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": ".ctor(TeamsManager.UI.Services.Abstractions.IMsalAuthService,TeamsManager.UI.Services.Abstractions.IManualTestingService,System.Net.Http.IHttpClientFactory,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.Views.ManualTestingWindow>)", "methodShortName": ".ctor(...)", "fileIndex": 1, "line": 37,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "ExecuteGraphTest()", "methodShortName": "ExecuteGraphTest()", "fileIndex": 1, "line": 804,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "ExecuteWhoAmITest()", "methodShortName": "ExecuteWhoAmITest()", "fileIndex": 1, "line": 758,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "VisualTreeExtensions", "reportPath": "TeamsManager.UI_VisualTreeExtensions.html", "methodName": "Descendants()", "methodShortName": "Descendants()", "fileIndex": 0, "line": 1163,
    "metrics": [
      { "value": 72, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.API.Controllers.TeamLifecycleController", "reportPath": "TeamsManager.Api_TeamLifecycleController.html", "methodName": "MigrateTeamsBetweenSchoolYears()", "methodShortName": "MigrateTeamsBetweenSchoolYears()", "fileIndex": 0, "line": 186,
    "metrics": [
      { "value": 69, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.DepartmentService", "reportPath": "TeamsManager.Core_DepartmentService.html", "methodName": "UpdateDepartmentAsync()", "methodShortName": "UpdateDepartmentAsync()", "fileIndex": 0, "line": 279,
    "metrics": [
      { "value": 57, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Extensions.EnumExtensions", "reportPath": "TeamsManager.Core_EnumExtensions.html", "methodName": "ToPolishString(TeamsManager.Core.Enums.SettingType)", "methodShortName": "ToPolishString(...)", "fileIndex": 0, "line": 130,
    "metrics": [
      { "value": 56, "exceeded": true },
      { "value": 7, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Extensions.EnumExtensions", "reportPath": "TeamsManager.Core_EnumExtensions.html", "methodName": "ToPolishString(TeamsManager.Core.Enums.SettingType)", "methodShortName": "ToPolishString(...)", "fileIndex": 0, "line": 129,
    "metrics": [
      { "value": 56, "exceeded": true },
      { "value": 7, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.BulkImportWizardViewModel", "reportPath": "TeamsManager.UI_BulkImportWizardViewModel.html", "methodName": "ValidateCurrentStep()", "methodShortName": "ValidateCurrentStep()", "fileIndex": 0, "line": 223,
    "metrics": [
      { "value": 56, "exceeded": true },
      { "value": 7, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamCreationWizardViewModel", "reportPath": "TeamsManager.UI_TeamCreationWizardViewModel.html", "methodName": "CanGoNext()", "methodShortName": "CanGoNext()", "fileIndex": 0, "line": 501,
    "metrics": [
      { "value": 56, "exceeded": true },
      { "value": 7, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ChannelService", "reportPath": "TeamsManager.Core_ChannelService.html", "methodName": "GetTeamChannelsAsync()", "methodShortName": "GetTeamChannelsAsync()", "fileIndex": 0, "line": 127,
    "metrics": [
      { "value": 55, "exceeded": true },
      { "value": 32, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "AssignUserToSchoolTypeAsync()", "methodShortName": "AssignUserToSchoolTypeAsync()", "fileIndex": 0, "line": 697,
    "metrics": [
      { "value": 50, "exceeded": true },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.API.Controllers.TeamLifecycleController", "reportPath": "TeamsManager.Api_TeamLifecycleController.html", "methodName": "BulkRestoreTeamsWithValidation()", "methodShortName": "BulkRestoreTeamsWithValidation()", "fileIndex": 0, "line": 114,
    "metrics": [
      { "value": 49, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.API.Controllers.TeamLifecycleController", "reportPath": "TeamsManager.Api_TeamLifecycleController.html", "methodName": "ConsolidateInactiveTeams()", "methodShortName": "ConsolidateInactiveTeams()", "fileIndex": 0, "line": 256,
    "metrics": [
      { "value": 48, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamsManagerHealthOrchestrator", "reportPath": "TeamsManager.Application_TeamsManagerHealthOrchestrator.html", "methodName": "CheckMicrosoftGraphApiHealthAsync()", "methodShortName": "CheckMicrosoftGraphApiHealthAsync()", "fileIndex": 0, "line": 413,
    "metrics": [
      { "value": 48, "exceeded": true },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.GraphChannel", "reportPath": "TeamsManager.Core_GraphChannel.html", "methodName": "GetDetailedInfo()", "methodShortName": "GetDetailedInfo()", "fileIndex": 0, "line": 245,
    "metrics": [
      { "value": 44, "exceeded": true },
      { "value": 44, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkAddUsersToTeamAsync()", "methodShortName": "BulkAddUsersToTeamAsync()", "fileIndex": 0, "line": 58,
    "metrics": [
      { "value": 44, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OperationHistoryService", "reportPath": "TeamsManager.Core_OperationHistoryService.html", "methodName": "UpdateOperationStatusAsync()", "methodShortName": "UpdateOperationStatusAsync()", "fileIndex": 0, "line": 78,
    "metrics": [
      { "value": 43, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.BulkUserManagementController", "reportPath": "TeamsManager.Api_BulkUserManagementController.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IBulkUserManagementOrchestrator,TeamsManager.Core.Abstractions.Services.Auth.ITokenManager,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Api.Controllers.BulkUserManagementController>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 25,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.ChannelsController", "reportPath": "TeamsManager.Api_ChannelsController.html", "methodName": "RemoveTeamChannel()", "methodShortName": "RemoveTeamChannel()", "fileIndex": 0, "line": 167,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.DiagnosticsController", "reportPath": "TeamsManager.Api_DiagnosticsController.html", "methodName": "GetConfigurationStatusAsync()", "methodShortName": "GetConfigurationStatusAsync()", "fileIndex": 0, "line": 436,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.HealthChecks.DependencyInjectionHealthCheck", "reportPath": "TeamsManager.Api_DependencyInjectionHealthCheck.html", "methodName": "CheckHealthAsync(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext,System.Threading.CancellationToken)", "methodShortName": "CheckHealthAsync(...)", "fileIndex": 0, "line": 26,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Hubs.MonitoringHub", "reportPath": "TeamsManager.Api_MonitoringHub.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IHealthMonitoringOrchestrator,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Api.Hubs.MonitoringHub>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 23,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Hubs.MonitoringHub", "reportPath": "TeamsManager.Api_MonitoringHub.html", "methodName": "GetMonitoringStats()", "methodShortName": "GetMonitoringStats()", "fileIndex": 0, "line": 262,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Hubs.MonitoringHub", "reportPath": "TeamsManager.Api_MonitoringHub.html", "methodName": "OnDisconnectedAsync()", "methodShortName": "OnDisconnectedAsync()", "fileIndex": 0, "line": 86,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Hubs.NotificationHub", "reportPath": "TeamsManager.Api_NotificationHub.html", "methodName": "GetConnectionStats()", "methodShortName": "GetConnectionStats()", "fileIndex": 0, "line": 215,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Hubs.NotificationHub", "reportPath": "TeamsManager.Api_NotificationHub.html", "methodName": "JoinNotificationGroup()", "methodShortName": "JoinNotificationGroup()", "fileIndex": 0, "line": 146,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "Dispose()", "methodShortName": "Dispose()", "fileIndex": 0, "line": 1481,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "BulkCreateUsersWithGraphApiAsync()", "methodShortName": "BulkCreateUsersWithGraphApiAsync()", "fileIndex": 0, "line": 1256,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.SchoolYearProcessOrchestrator", "reportPath": "TeamsManager.Application_SchoolYearProcessOrchestrator.html", "methodName": "Dispose()", "methodShortName": "Dispose()", "fileIndex": 0, "line": 562,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "Dispose()", "methodShortName": "Dispose()", "fileIndex": 0, "line": 1095,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "Dispose()", "methodShortName": "Dispose()", "fileIndex": 0, "line": 1080,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "ArchiveTeamWithGraphApiAsync()", "methodShortName": "ArchiveTeamWithGraphApiAsync()", "fileIndex": 0, "line": 891,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "ProcessArchiveBatchAsync()", "methodShortName": "ProcessArchiveBatchAsync()", "fileIndex": 0, "line": 819,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": "RestoreTeamWithGraphApiAsync()", "methodShortName": "RestoreTeamWithGraphApiAsync()", "fileIndex": 0, "line": 930,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamsManagerHealthOrchestrator", "reportPath": "TeamsManager.Application_TeamsManagerHealthOrchestrator.html", "methodName": "RepairGraphConnectionAsync()", "methodShortName": "RepairGraphConnectionAsync()", "fileIndex": 0, "line": 664,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphApiException", "reportPath": "TeamsManager.Core_GraphApiException.html", "methodName": "GetMetadata(System.String)", "methodShortName": "GetMetadata(...)", "fileIndex": 0, "line": 198,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Exceptions.Graph.GraphRateLimitException", "reportPath": "TeamsManager.Core_GraphRateLimitException.html", "methodName": "get_UsagePercentage()", "methodShortName": "get_UsagePercentage()", "fileIndex": 0, "line": 98,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Channel", "reportPath": "TeamsManager.Core_Channel.html", "methodName": "get_FilesSizeFormatted()", "methodShortName": "get_FilesSizeFormatted()", "fileIndex": 0, "line": 200,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.GraphBulkResult", "reportPath": "TeamsManager.Core_GraphBulkResult.html", "methodName": "GetSummary()", "methodShortName": "GetSummary()", "fileIndex": 0, "line": 347,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.GraphCacheRateLimitInfo", "reportPath": "TeamsManager.Core_GraphCacheRateLimitInfo.html", "methodName": "get_CanMakeRequest()", "methodShortName": "get_CanMakeRequest()", "fileIndex": 0, "line": 95,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.GraphOperationResult<T>", "reportPath": "TeamsManager.Core_GraphOperationResult_1.html", "methodName": "GetSummary()", "methodShortName": "GetSummary()", "fileIndex": 0, "line": 436,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.TeamMember", "reportPath": "TeamsManager.Core_TeamMember.html", "methodName": "get_MembershipStatus()", "methodShortName": "get_MembershipStatus()", "fileIndex": 0, "line": 211,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.TeamMember", "reportPath": "TeamsManager.Core_TeamMember.html", "methodName": "HasPermission(System.String)", "methodShortName": "HasPermission(...)", "fileIndex": 0, "line": 336,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ApplicationSettingService", "reportPath": "TeamsManager.Core_ApplicationSettingService.html", "methodName": "DeleteSettingAsync()", "methodShortName": "DeleteSettingAsync()", "fileIndex": 0, "line": 438,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ApplicationSettingService", "reportPath": "TeamsManager.Core_ApplicationSettingService.html", "methodName": "GetAllSettingsAsync()", "methodShortName": "GetAllSettingsAsync()", "fileIndex": 0, "line": 148,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Cache.CacheInvalidationService", "reportPath": "TeamsManager.Core_CacheInvalidationService.html", "methodName": "InvalidateForDepartmentChangedAsync()", "methodShortName": "InvalidateForDepartmentChangedAsync()", "fileIndex": 0, "line": 402,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Cache.CacheInvalidationService", "reportPath": "TeamsManager.Core_CacheInvalidationService.html", "methodName": "InvalidateForTeamArchivedAsync()", "methodShortName": "InvalidateForTeamArchivedAsync()", "fileIndex": 0, "line": 112,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Cache.CascadeInvalidationStrategy", "reportPath": "TeamsManager.Core_CascadeInvalidationStrategy.html", "methodName": "GetCascadeKeys(System.String,System.Object)", "methodShortName": "GetCascadeKeys(...)", "fileIndex": 0, "line": 553,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ChannelService", "reportPath": "TeamsManager.Core_ChannelService.html", "methodName": "InvalidateAllChannelsForTeamAsync()", "methodShortName": "InvalidateAllChannelsForTeamAsync()", "fileIndex": 0, "line": 715,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.DepartmentService", "reportPath": "TeamsManager.Core_DepartmentService.html", "methodName": "IsDescendantAsync()", "methodShortName": "IsDescendantAsync()", "fileIndex": 0, "line": 608,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "ExtendTtl(System.String,System.TimeSpan)", "methodShortName": "ExtendTtl(...)", "fileIndex": 0, "line": 708,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "InvalidateByPattern(System.String,System.String)", "methodShortName": "InvalidateByPattern(...)", "fileIndex": 0, "line": 1067,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "ExtractEndpointFromCacheKey(System.String)", "methodShortName": "ExtractEndpointFromCacheKey(...)", "fileIndex": 0, "line": 1311,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphConnectionService", "reportPath": "TeamsManager.Core_GraphConnectionService.html", "methodName": "TestSingleEndpoint()", "methodShortName": "TestSingleEndpoint()", "fileIndex": 0, "line": 684,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphService", "reportPath": "TeamsManager.Core_GraphService.html", "methodName": "ExecuteBatchOperationAsync()", "methodShortName": "ExecuteBatchOperationAsync()", "fileIndex": 0, "line": 222,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "ArchiveTeamAsync()", "methodShortName": "ArchiveTeamAsync()", "fileIndex": 0, "line": 251,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "DeleteTeamAsync()", "methodShortName": "DeleteTeamAsync()", "fileIndex": 0, "line": 341,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "UnarchiveTeamAsync()", "methodShortName": "UnarchiveTeamAsync()", "fileIndex": 0, "line": 296,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTeamManagementService", "reportPath": "TeamsManager.Core_GraphTeamManagementService.html", "methodName": "UnarchiveTeamAsync()", "methodShortName": "UnarchiveTeamAsync()", "fileIndex": 0, "line": 306,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphTokenManager", "reportPath": "TeamsManager.Core_GraphTokenManager.html", "methodName": ".ctor(Microsoft.Identity.Client.IConfidentialClientApplication,Microsoft.Extensions.Caching.Memory.IMemoryCache,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.Graph.GraphTokenManager>,Microsoft.Extensions.Configuration.IConfiguration,TeamsManager.Core.Models.Graph.GraphApiConfiguration)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 31,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "GetAllUsersAsync()", "methodShortName": "GetAllUsersAsync()", "fileIndex": 0, "line": 486,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "GetInactiveUsersAsync()", "methodShortName": "GetInactiveUsersAsync()", "fileIndex": 0, "line": 530,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphUserManagementService", "reportPath": "TeamsManager.Core_GraphUserManagementService.html", "methodName": "SetM365UserAccountStateAsync()", "methodShortName": "SetM365UserAccountStateAsync()", "fileIndex": 0, "line": 199,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.GraphAdminNotificationService", "reportPath": "TeamsManager.Core_GraphAdminNotificationService.html", "methodName": "SendCriticalErrorNotificationAsync()", "methodShortName": "SendCriticalErrorNotificationAsync()", "fileIndex": 0, "line": 174,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.GraphAdminNotificationService", "reportPath": "TeamsManager.Core_GraphAdminNotificationService.html", "methodName": "SendEmailAsync()", "methodShortName": "SendEmailAsync()", "fileIndex": 0, "line": 379,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ModernHttpService", "reportPath": "TeamsManager.Core_ModernHttpService.html", "methodName": "GetHttpClient(System.String)", "methodShortName": "GetHttpClient(...)", "fileIndex": 0, "line": 159,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ModernHttpService", "reportPath": "TeamsManager.Core_ModernHttpService.html", "methodName": "CreateDraftEmailAsync()", "methodShortName": "CreateDraftEmailAsync()", "fileIndex": 0, "line": 508,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ModernHttpService", "reportPath": "TeamsManager.Core_ModernHttpService.html", "methodName": "GetMailMessageAsync()", "methodShortName": "GetMailMessageAsync()", "fileIndex": 0, "line": 649,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ModernHttpService", "reportPath": "TeamsManager.Core_ModernHttpService.html", "methodName": "PutAsync()", "methodShortName": "PutAsync()", "fileIndex": 0, "line": 1122,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ModernHttpService", "reportPath": "TeamsManager.Core_ModernHttpService.html", "methodName": "SendMailAsync()", "methodShortName": "SendMailAsync()", "fileIndex": 0, "line": 380,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "GetDepartmentsByOrganizationalUnitAsync()", "methodShortName": "GetDepartmentsByOrganizationalUnitAsync()", "fileIndex": 0, "line": 739,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "GetOrganizationalUnitsHierarchyAsync()", "methodShortName": "GetOrganizationalUnitsHierarchyAsync()", "fileIndex": 0, "line": 182,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.OrganizationalUnitService", "reportPath": "TeamsManager.Core_OrganizationalUnitService.html", "methodName": "GetSubUnitsAsync()", "methodShortName": "GetSubUnitsAsync()", "fileIndex": 0, "line": 149,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolTypeService", "reportPath": "TeamsManager.Core_SchoolTypeService.html", "methodName": "DeleteSchoolTypeAsync()", "methodShortName": "DeleteSchoolTypeAsync()", "fileIndex": 0, "line": 366,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolTypeService", "reportPath": "TeamsManager.Core_SchoolTypeService.html", "methodName": "GetAllActiveSchoolTypesAsync()", "methodShortName": "GetAllActiveSchoolTypesAsync()", "fileIndex": 0, "line": 103,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolTypeService", "reportPath": "TeamsManager.Core_SchoolTypeService.html", "methodName": "RemoveViceDirectorFromSchoolTypeAsync()", "methodShortName": "RemoveViceDirectorFromSchoolTypeAsync()", "fileIndex": 0, "line": 528,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SeedDataService", "reportPath": "TeamsManager.Core_SeedDataService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.OrganizationalUnit>,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.Department>,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.SeedDataService>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 26,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SubjectService", "reportPath": "TeamsManager.Core_SubjectService.html", "methodName": "GetAllActiveSubjectsAsync()", "methodShortName": "GetAllActiveSubjectsAsync()", "fileIndex": 0, "line": 115,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.GraphSynchronizerBase<T1, T2>", "reportPath": "TeamsManager.Core_GraphSynchronizerBase_2.html", "methodName": "RequiresSynchronizationAsync()", "methodShortName": "RequiresSynchronizationAsync()", "fileIndex": 0, "line": 71,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Synchronization.TeamSynchronizer", "reportPath": "TeamsManager.Core_TeamSynchronizer.html", "methodName": "ParseVisibility(System.String)", "methodShortName": "ParseVisibility(...)", "fileIndex": 0, "line": 247,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "MapVisibility(System.String)", "methodShortName": "MapVisibility(...)", "fileIndex": 0, "line": 1374,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "<CreateTeamAsync()", "methodShortName": "<CreateTeamAsync()", "fileIndex": 0, "line": 490,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "DeleteTeamAsync()", "methodShortName": "DeleteTeamAsync()", "fileIndex": 0, "line": 945,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "DeleteTemplateAsync()", "methodShortName": "DeleteTemplateAsync()", "fileIndex": 0, "line": 605,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "GetDefaultTemplateForSchoolTypeAsync()", "methodShortName": "GetDefaultTemplateForSchoolTypeAsync()", "fileIndex": 0, "line": 192,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": "GetUniversalTemplatesAsync()", "methodShortName": "GetUniversalTemplatesAsync()", "fileIndex": 0, "line": 146,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": "GetAllActiveUsersAsync()", "methodShortName": "GetAllActiveUsersAsync()", "fileIndex": 0, "line": 224,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.BooleanToOpacityConverter", "reportPath": "TeamsManager.UI_BooleanToOpacityConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.BooleanToYesNoConverter", "reportPath": "TeamsManager.UI_BooleanToYesNoConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 13,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.ColorToBrushConverter", "reportPath": "TeamsManager.UI_ColorToBrushConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.EqualToVisibilityConverter", "reportPath": "TeamsManager.UI_EqualToVisibilityConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 15,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.IntToVisibilityConverter", "reportPath": "TeamsManager.UI_IntToVisibilityConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 15,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.InverseBooleanToVisibilityConverter", "reportPath": "TeamsManager.UI_InverseBooleanToVisibilityConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 11,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.InverseBoolToVisibilityConverter", "reportPath": "TeamsManager.UI_InverseBoolToVisibilityConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 15,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.TeamMemberRoleToStringConverter", "reportPath": "TeamsManager.UI_TeamMemberRoleToStringConverter.html", "methodName": "ConvertBack(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "ConvertBack(...)", "fileIndex": 0, "line": 24,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.TeamStatusToArchiveVisibilityConverter", "reportPath": "TeamsManager.UI_TeamStatusToArchiveVisibilityConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 12,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Converters.TeamStatusToRestoreVisibilityConverter", "reportPath": "TeamsManager.UI_TeamStatusToRestoreVisibilityConverter.html", "methodName": "Convert(System.Object,System.Type,System.Object,System.Globalization.CultureInfo)", "methodShortName": "Convert(...)", "fileIndex": 0, "line": 29,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "set_FirstSemesterStart(System.Nullable`1<System.DateTime>)", "methodShortName": "set_FirstSemesterStart(...)", "fileIndex": 0, "line": 104,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "set_FirstSemesterEnd(System.Nullable`1<System.DateTime>)", "methodShortName": "set_FirstSemesterEnd(...)", "fileIndex": 0, "line": 119,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "set_SecondSemesterStart(System.Nullable`1<System.DateTime>)", "methodShortName": "set_SecondSemesterStart(...)", "fileIndex": 0, "line": 134,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "set_SecondSemesterEnd(System.Nullable`1<System.DateTime>)", "methodShortName": "set_SecondSemesterEnd(...)", "fileIndex": 0, "line": 149,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "get_Status()", "methodShortName": "get_Status()", "fileIndex": 0, "line": 178,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "get_CompletionPercentage()", "methodShortName": "get_CompletionPercentage()", "fileIndex": 0, "line": 212,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "get_HasSemesters()", "methodShortName": "get_HasSemesters()", "fileIndex": 0, "line": 265,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.SchoolYearModels.SchoolYearDisplayModel", "reportPath": "TeamsManager.UI_SchoolYearDisplayModel.html", "methodName": "get_TimeRemaining()", "methodShortName": "get_TimeRemaining()", "fileIndex": 0, "line": 291,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.SchoolTypeAssignmentModel", "reportPath": "TeamsManager.UI_SchoolTypeAssignmentModel.html", "methodName": "get_IsValid()", "methodShortName": "get_IsValid()", "fileIndex": 0, "line": 148,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "set_FirstName(System.String)", "methodShortName": "set_FirstName(...)", "fileIndex": 0, "line": 36,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "set_LastName(System.String)", "methodShortName": "set_LastName(...)", "fileIndex": 0, "line": 52,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "RemovePolishCharacters(System.String)", "methodShortName": "RemovePolishCharacters(...)", "fileIndex": 0, "line": 173,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "ValidateFirstName()", "methodShortName": "ValidateFirstName()", "fileIndex": 0, "line": 266,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "ValidateLastName()", "methodShortName": "ValidateLastName()", "fileIndex": 0, "line": 277,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Models.ViewModels.UserDetailModel", "reportPath": "TeamsManager.UI_UserDetailModel.html", "methodName": "ValidatePhone()", "methodShortName": "ValidatePhone()", "fileIndex": 0, "line": 319,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Scripts.CreateDefaultOrganizationalUnit", "reportPath": "TeamsManager.UI_CreateDefaultOrganizationalUnit.html", "methodName": "ExecuteAsync()", "methodShortName": "ExecuteAsync()", "fileIndex": 0, "line": 14,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Scripts.TestDepartmentCRUD", "reportPath": "TeamsManager.UI_TestDepartmentCRUD.html", "methodName": "TestCodeMigrationAsync()", "methodShortName": "TestCodeMigrationAsync()", "fileIndex": 0, "line": 293,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Scripts.TestDepartmentCRUD", "reportPath": "TeamsManager.UI_TestDepartmentCRUD.html", "methodName": "TestUpdateOperationsAsync()", "methodShortName": "TestUpdateOperationsAsync()", "fileIndex": 0, "line": 165,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.ApplicationSettingService", "reportPath": "TeamsManager.UI_ApplicationSettingService.html", "methodName": "GetSettingByKeyAsync()", "methodShortName": "GetSettingByKeyAsync()", "fileIndex": 0, "line": 85,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.ConditionalAccessAnalyzer", "reportPath": "TeamsManager.UI_ConditionalAccessAnalyzer.html", "methodName": "ExtractAllClaims(System.IdentityModel.Tokens.Jwt.JwtSecurityToken)", "methodShortName": "ExtractAllClaims(...)", "fileIndex": 0, "line": 191,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Configuration.ConfigurationValidator", "reportPath": "TeamsManager.UI_ConfigurationValidator.html", "methodName": "IsValidApiAudience(System.String)", "methodShortName": "IsValidApiAudience(...)", "fileIndex": 0, "line": 308,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.Dashboard.SimpleDashboardOperationHistoryService", "reportPath": "TeamsManager.UI_SimpleDashboardOperationHistoryService.html", "methodName": "CreateNewOperationEntryAsync(TeamsManager.Core.Enums.OperationType,System.String,System.String,System.String,System.String,System.String)", "methodShortName": "CreateNewOperationEntryAsync(...)", "fileIndex": 0, "line": 150,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.DepartmentCodeMigrationService", "reportPath": "TeamsManager.UI_DepartmentCodeMigrationService.html", "methodName": "NormalizeDepartmentName(System.String)", "methodShortName": "NormalizeDepartmentName(...)", "fileIndex": 0, "line": 195,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.GraphUserProfileService", "reportPath": "TeamsManager.UI_GraphUserProfileService.html", "methodName": ".ctor(System.Net.Http.HttpClient,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.Services.GraphUserProfileService>,TeamsManager.Core.Models.Graph.GraphApiConfiguration)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 27,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringPerformanceOptimizer", "reportPath": "TeamsManager.UI_MonitoringPerformanceOptimizer.html", "methodName": "PushAlert(TeamsManager.UI.Models.Monitoring.SystemAlert)", "methodShortName": "PushAlert(...)", "fileIndex": 0, "line": 182,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringPerformanceOptimizer", "reportPath": "TeamsManager.UI_MonitoringPerformanceOptimizer.html", "methodName": "GetStatistics()", "methodShortName": "GetStatistics()", "fileIndex": 0, "line": 229,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MonitoringPerformanceOptimizer", "reportPath": "TeamsManager.UI_MonitoringPerformanceOptimizer.html", "methodName": "Dispose()", "methodShortName": "Dispose()", "fileIndex": 0, "line": 382,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MsalAuthService", "reportPath": "TeamsManager.UI_MsalAuthService.html", "methodName": "AcquireTokenSilentAsync()", "methodShortName": "AcquireTokenSilentAsync()", "fileIndex": 0, "line": 271,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MsalAuthService", "reportPath": "TeamsManager.UI_MsalAuthService.html", "methodName": "AcquireTokenSilentAsync()", "methodShortName": "AcquireTokenSilentAsync()", "fileIndex": 0, "line": 265,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.MsalCacheHelper", "reportPath": "TeamsManager.UI_MsalCacheHelper.html", "methodName": "ClearTokenCacheAsync()", "methodShortName": "ClearTokenCacheAsync()", "fileIndex": 0, "line": 104,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.SimpleUserService", "reportPath": "TeamsManager.UI_SimpleUserService.html", "methodName": ".ctor(TeamsManager.Data.TeamsManagerDbContext,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.Services.SimpleUserService>,TeamsManager.Core.Services.SeedDataService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 25,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.SimpleUserService", "reportPath": "TeamsManager.UI_SimpleUserService.html", "methodName": "CreateUserAsync()", "methodShortName": "CreateUserAsync()", "fileIndex": 0, "line": 150,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.SimpleUserService", "reportPath": "TeamsManager.UI_SimpleUserService.html", "methodName": "UpdateUserAsync()", "methodShortName": "UpdateUserAsync()", "fileIndex": 0, "line": 184,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.TeamsManagerApiService", "reportPath": "TeamsManager.UI_TeamsManagerApiService.html", "methodName": ".ctor(System.Net.Http.HttpClient,TeamsManager.UI.Services.Abstractions.IMsalAuthService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.Services.TeamsManagerApiService>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 56,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.DepartmentTreeService", "reportPath": "TeamsManager.UI_DepartmentTreeService.html", "methodName": "FindItemById(System.Collections.Generic.IEnumerable`1<TeamsManager.UI.ViewModels.Departments.DepartmentTreeItemViewModel>,System.String)", "methodShortName": "FindItemById(...)", "fileIndex": 0, "line": 60,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.DepartmentTreeService", "reportPath": "TeamsManager.UI_DepartmentTreeService.html", "methodName": "CanMoveDepartment(TeamsManager.UI.ViewModels.Departments.DepartmentTreeItemViewModel,TeamsManager.UI.ViewModels.Departments.DepartmentTreeItemViewModel)", "methodShortName": "CanMoveDepartment(...)", "fileIndex": 0, "line": 78,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.DepartmentTreeService", "reportPath": "TeamsManager.UI_DepartmentTreeService.html", "methodName": "IsDescendant(TeamsManager.UI.ViewModels.Departments.DepartmentTreeItemViewModel,TeamsManager.UI.ViewModels.Departments.DepartmentTreeItemViewModel)", "methodShortName": "IsDescendant(...)", "fileIndex": 0, "line": 91,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.DepartmentTreeService", "reportPath": "TeamsManager.UI_DepartmentTreeService.html", "methodName": "SortTree(System.Collections.Generic.List`1<TeamsManager.UI.ViewModels.Departments.DepartmentTreeItemViewModel>)", "methodShortName": "SortTree(...)", "fileIndex": 0, "line": 105,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UI.SchoolYearUIService", "reportPath": "TeamsManager.UI_SchoolYearUIService.html", "methodName": "CanDeleteSchoolYearAsync()", "methodShortName": "CanDeleteSchoolYearAsync()", "fileIndex": 0, "line": 338,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UIDialogService", "reportPath": "TeamsManager.UI_UIDialogService.html", "methodName": "ShowLoadingOverlay(System.String)", "methodShortName": "ShowLoadingOverlay(...)", "fileIndex": 0, "line": 192,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Services.UIDialogService", "reportPath": "TeamsManager.UI_UIDialogService.html", "methodName": "HideLoadingOverlay()", "methodShortName": "HideLoadingOverlay()", "fileIndex": 0, "line": 214,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.UserControls.Import.FileSelectionStep", "reportPath": "TeamsManager.UI_FileSelectionStep.html", "methodName": "OnFileDrop(System.Object,System.Windows.DragEventArgs)", "methodShortName": "OnFileDrop(...)", "fileIndex": 1, "line": 20,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.UserControls.Import.FileSelectionStep", "reportPath": "TeamsManager.UI_FileSelectionStep.html", "methodName": "OnDragEnter(System.Object,System.Windows.DragEventArgs)", "methodShortName": "OnDragEnter(...)", "fileIndex": 1, "line": 42,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.UserControls.Import.FileSelectionStep", "reportPath": "TeamsManager.UI_FileSelectionStep.html", "methodName": "SetDropBorderHighlight(System.Boolean,System.Boolean)", "methodShortName": "SetDropBorderHighlight(...)", "fileIndex": 1, "line": 91,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dashboard.DashboardViewModel", "reportPath": "TeamsManager.UI_DashboardViewModel.html", "methodName": "CalculateActivity()", "methodShortName": "CalculateActivity()", "fileIndex": 0, "line": 324,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "get_OrganizationalUnitId()", "methodShortName": "get_OrganizationalUnitId()", "fileIndex": 0, "line": 368,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "set_OrganizationalUnitId(System.String)", "methodShortName": "set_OrganizationalUnitId(...)", "fileIndex": 0, "line": 370,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "NormalizeDepartmentName(System.String)", "methodShortName": "NormalizeDepartmentName(...)", "fileIndex": 0, "line": 794,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentEditViewModel", "reportPath": "TeamsManager.UI_DepartmentEditViewModel.html", "methodName": "GenerateAndValidateCodeAsync()", "methodShortName": "GenerateAndValidateCodeAsync()", "fileIndex": 0, "line": 897,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": "FindDepartmentById(System.String,System.Collections.ObjectModel.ObservableCollection`1<TeamsManager.UI.ViewModels.Departments.DepartmentTreeItemViewModel>)", "methodShortName": "FindDepartmentById(...)", "fileIndex": 0, "line": 514,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": "FilterOrganizationalUnits(System.Object)", "methodShortName": "FilterOrganizationalUnits(...)", "fileIndex": 0, "line": 691,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Departments.DepartmentsManagementViewModel", "reportPath": "TeamsManager.UI_DepartmentsManagementViewModel.html", "methodName": "FindItemById(System.String,System.Collections.ObjectModel.ObservableCollection`1<TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitTreeItemViewModel>)", "methodShortName": "FindItemById(...)", "fileIndex": 0, "line": 754,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "reportPath": "TeamsManager.UI_UniversalDialogViewModel.html", "methodName": "get_HasLongContent()", "methodShortName": "get_HasLongContent()", "fileIndex": 0, "line": 101,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "reportPath": "TeamsManager.UI_UniversalDialogViewModel.html", "methodName": "OnPrimaryClick()", "methodShortName": "OnPrimaryClick()", "fileIndex": 0, "line": 285,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Dialogs.UniversalDialogViewModel", "reportPath": "TeamsManager.UI_UniversalDialogViewModel.html", "methodName": "OnSecondaryClick()", "methodShortName": "OnSecondaryClick()", "fileIndex": 0, "line": 297,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportFileSelectionViewModel", "reportPath": "TeamsManager.UI_ImportFileSelectionViewModel.html", "methodName": "IsFileFormatValid()", "methodShortName": "IsFileFormatValid()", "fileIndex": 0, "line": 299,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportProgressViewModel", "reportPath": "TeamsManager.UI_ImportProgressViewModel.html", "methodName": "SimulateProgress()", "methodShortName": "SimulateProgress()", "fileIndex": 0, "line": 335,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportProgressViewModel", "reportPath": "TeamsManager.UI_ImportProgressViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IDataImportOrchestrator,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Import.ImportProgressViewModel>,TeamsManager.UI.Services.Abstractions.IUIDialogService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 26,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportProgressViewModel", "reportPath": "TeamsManager.UI_ImportProgressViewModel.html", "methodName": "PollImportStatusAsync()", "methodShortName": "PollImportStatusAsync()", "fileIndex": 0, "line": 312,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportValidationViewModel", "reportPath": "TeamsManager.UI_ImportValidationViewModel.html", "methodName": "FixItemAsync()", "methodShortName": "FixItemAsync()", "fileIndex": 0, "line": 434,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Import.ImportValidationViewModel", "reportPath": "TeamsManager.UI_ImportValidationViewModel.html", "methodName": "TryFixErrorAsync()", "methodShortName": "TryFixErrorAsync()", "fileIndex": 0, "line": 358,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.ActiveOperationsWidgetViewModel", "reportPath": "TeamsManager.UI_ActiveOperationsWidgetViewModel.html", "methodName": ".ctor(TeamsManager.UI.Services.IMonitoringDataService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Monitoring.Widgets.ActiveOperationsWidgetViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 37,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.AdvancedPerformanceChartWidgetViewModel", "reportPath": "TeamsManager.UI_AdvancedPerformanceChartWidgetViewModel.html", "methodName": ".ctor(TeamsManager.UI.Services.ISignalRService,TeamsManager.UI.Services.IMonitoringDataService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Monitoring.Widgets.AdvancedPerformanceChartWidgetViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 35,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.AlertsWidgetViewModel", "reportPath": "TeamsManager.UI_AlertsWidgetViewModel.html", "methodName": ".ctor(TeamsManager.UI.Services.IMonitoringDataService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Monitoring.Widgets.AlertsWidgetViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 45,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Monitoring.Widgets.TeamsManagerHealthWidgetViewModel", "reportPath": "TeamsManager.UI_TeamsManagerHealthWidgetViewModel.html", "methodName": "CalculateOverallStatus()", "methodShortName": "CalculateOverallStatus()", "fileIndex": 0, "line": 189,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryItemViewModel", "reportPath": "TeamsManager.UI_OperationHistoryItemViewModel.html", "methodName": "get_DisplayUser()", "methodShortName": "get_DisplayUser()", "fileIndex": 0, "line": 131,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Operations.OperationHistoryViewModel", "reportPath": "TeamsManager.UI_OperationHistoryViewModel.html", "methodName": "BuildOperationDetailsText(TeamsManager.UI.ViewModels.Operations.OperationHistoryItemViewModel)", "methodShortName": "BuildOperationDetailsText(...)", "fileIndex": 0, "line": 785,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "GenerateAndValidateCodeAsync()", "methodShortName": "GenerateAndValidateCodeAsync()", "fileIndex": 0, "line": 1175,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "GetAllChildUnitIdsRecursivelyAsync()", "methodShortName": "GetAllChildUnitIdsRecursivelyAsync()", "fileIndex": 0, "line": 710,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "InitializeForEditAsync()", "methodShortName": "InitializeForEditAsync()", "fileIndex": 0, "line": 591,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "SaveAsync()", "methodShortName": "SaveAsync()", "fileIndex": 0, "line": 844,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitEditViewModel.html", "methodName": "ValidateNameConflictAsync()", "methodShortName": "ValidateNameConflictAsync()", "fileIndex": 0, "line": 815,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitsManagementViewModel.html", "methodName": "FindOrganizationalUnitById(System.String,System.Collections.ObjectModel.ObservableCollection`1<TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitTreeItemViewModel>)", "methodShortName": "FindOrganizationalUnitById(...)", "fileIndex": 0, "line": 477,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitsManagementViewModel.html", "methodName": "EditOrganizationalUnitAsync()", "methodShortName": "EditOrganizationalUnitAsync()", "fileIndex": 0, "line": 326,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitTreeItemViewModel", "reportPath": "TeamsManager.UI_OrganizationalUnitTreeItemViewModel.html", "methodName": "get_ToolTip()", "methodShortName": "get_ToolTip()", "fileIndex": 0, "line": 152,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.RelayCommand<T>", "reportPath": "TeamsManager.UI_RelayCommand_1.html", "methodName": "CanExecute(System.Object)", "methodShortName": "CanExecute(...)", "fileIndex": 0, "line": 80,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypeEditViewModel", "reportPath": "TeamsManager.UI_SchoolTypeEditViewModel.html", "methodName": "CanSave()", "methodShortName": "CanSave()", "fileIndex": 0, "line": 198,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypesListViewModel", "reportPath": "TeamsManager.UI_SchoolTypesListViewModel.html", "methodName": ".ctor(TeamsManager.UI.Services.UI.SchoolTypeUIService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypesListViewModel>,TeamsManager.UI.ViewModels.Shell.MainShellViewModel)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 31,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.SchoolTypes.SchoolTypesListViewModel", "reportPath": "TeamsManager.UI_SchoolTypesListViewModel.html", "methodName": "DeleteSchoolTypeAsync()", "methodShortName": "DeleteSchoolTypeAsync()", "fileIndex": 0, "line": 249,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.SchoolYears.SchoolYearListViewModel", "reportPath": "TeamsManager.UI_SchoolYearListViewModel.html", "methodName": "SetAsCurrentAsync()", "methodShortName": "SetAsCurrentAsync()", "fileIndex": 0, "line": 307,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingItemViewModel", "reportPath": "TeamsManager.UI_ApplicationSettingItemViewModel.html", "methodName": "ResetToDefault()", "methodShortName": "ResetToDefault()", "fileIndex": 0, "line": 207,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Settings.ApplicationSettingsViewModel", "reportPath": "TeamsManager.UI_ApplicationSettingsViewModel.html", "methodName": "LoadSettingsAsync()", "methodShortName": "LoadSettingsAsync()", "fileIndex": 0, "line": 197,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Shell.MainShellViewModel", "reportPath": "TeamsManager.UI_MainShellViewModel.html", "methodName": "LoadUserInfo()", "methodShortName": "LoadUserInfo()", "fileIndex": 0, "line": 312,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectEditViewModel", "reportPath": "TeamsManager.UI_SubjectEditViewModel.html", "methodName": "LoadSchoolTypesAsync()", "methodShortName": "LoadSchoolTypesAsync()", "fileIndex": 0, "line": 229,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectImportViewModel", "reportPath": "TeamsManager.UI_SubjectImportViewModel.html", "methodName": "FormatFileSize(System.Int64)", "methodShortName": "FormatFileSize(...)", "fileIndex": 0, "line": 453,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectsViewModel", "reportPath": "TeamsManager.UI_SubjectsViewModel.html", "methodName": "LoadSubjectsAsync()", "methodShortName": "LoadSubjectsAsync()", "fileIndex": 0, "line": 210,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Subjects.SubjectsViewModel", "reportPath": "TeamsManager.UI_SubjectsViewModel.html", "methodName": "ViewTeachersAsync()", "methodShortName": "ViewTeachersAsync()", "fileIndex": 0, "line": 430,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.ChannelCardViewModel", "reportPath": "TeamsManager.UI_ChannelCardViewModel.html", "methodName": "get_ChannelIcon()", "methodShortName": "get_ChannelIcon()", "fileIndex": 0, "line": 134,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.ChannelCardViewModel", "reportPath": "TeamsManager.UI_ChannelCardViewModel.html", "methodName": "get_StatusColor()", "methodShortName": "get_StatusColor()", "fileIndex": 0, "line": 161,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamListViewModel", "reportPath": "TeamsManager.UI_TeamListViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.ITeamService,TeamsManager.Core.Abstractions.Services.ISchoolTypeService,TeamsManager.Core.Abstractions.Services.ISchoolYearService,TeamsManager.Core.Abstractions.ICurrentUserService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.UI.ViewModels.Shell.MainShellViewModel,TeamsManager.UI.Services.Abstractions.IMsalAuthService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Teams.TeamListViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 33,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamMembersViewModel", "reportPath": "TeamsManager.UI_TeamMembersViewModel.html", "methodName": "FilterUsers(System.Object)", "methodShortName": "FilterUsers(...)", "fileIndex": 0, "line": 194,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamMembersViewModel", "reportPath": "TeamsManager.UI_TeamMembersViewModel.html", "methodName": "AddUserToTeamAsync()", "methodShortName": "AddUserToTeamAsync()", "fileIndex": 0, "line": 208,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamMembersViewModel", "reportPath": "TeamsManager.UI_TeamMembersViewModel.html", "methodName": "BulkImportMembersAsync()", "methodShortName": "BulkImportMembersAsync()", "fileIndex": 0, "line": 321,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamMembersViewModel", "reportPath": "TeamsManager.UI_TeamMembersViewModel.html", "methodName": "ParseUserListAsync()", "methodShortName": "ParseUserListAsync()", "fileIndex": 0, "line": 386,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamTemplateEditorViewModel", "reportPath": "TeamsManager.UI_TeamTemplateEditorViewModel.html", "methodName": "UpdatePreview()", "methodShortName": "UpdatePreview()", "fileIndex": 0, "line": 390,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Teams.TeamTemplateEditorViewModel", "reportPath": "TeamsManager.UI_TeamTemplateEditorViewModel.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.ITeamTemplateService,TeamsManager.Core.Abstractions.Services.ISchoolTypeService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.UI.ViewModels.Teams.TeamTemplateEditorViewModel>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 25,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserDetailViewModel", "reportPath": "TeamsManager.UI_UserDetailViewModel.html", "methodName": "set_Model(TeamsManager.UI.Models.ViewModels.UserDetailModel)", "methodShortName": "set_Model(...)", "fileIndex": 0, "line": 73,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserDetailViewModel", "reportPath": "TeamsManager.UI_UserDetailViewModel.html", "methodName": "GetValidationErrors()", "methodShortName": "GetValidationErrors()", "fileIndex": 0, "line": 450,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserListViewModel", "reportPath": "TeamsManager.UI_UserListViewModel.html", "methodName": "CreateNewUser()", "methodShortName": "CreateNewUser()", "fileIndex": 0, "line": 698,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.ViewModels.Users.UserSchoolTypeAssignmentViewModel", "reportPath": "TeamsManager.UI_UserSchoolTypeAssignmentViewModel.html", "methodName": "get_CanAddAssignment()", "methodShortName": "get_CanAddAssignment()", "fileIndex": 0, "line": 142,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.Dialogs.UniversalDialog", "reportPath": "TeamsManager.UI_UniversalDialog.html", "methodName": "OnLoaded(System.Object,System.Windows.RoutedEventArgs)", "methodShortName": "OnLoaded(...)", "fileIndex": 2, "line": 27,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "SetAuthenticationContext(Microsoft.Identity.Client.AuthenticationResult)", "methodShortName": "SetAuthenticationContext(...)", "fileIndex": 1, "line": 119,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "DisplayTestDetails(TeamsManager.UI.Models.TestCase)", "methodShortName": "DisplayTestDetails(...)", "fileIndex": 1, "line": 505,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.ManualTestingWindow", "reportPath": "TeamsManager.UI_ManualTestingWindow.html", "methodName": "MarkResult_Click()", "methodShortName": "MarkResult_Click()", "fileIndex": 1, "line": 1101,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.UI", "class": "TeamsManager.UI.Views.SchoolTypes.SchoolTypesListView", "reportPath": "TeamsManager.UI_SchoolTypesListView.html", "methodName": "UserControl_Loaded()", "methodShortName": "UserControl_Loaded()", "fileIndex": 1, "line": 31,
    "metrics": [
      { "value": 42, "exceeded": true },
      { "value": 6, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "GetExpiringEntries(System.TimeSpan)", "methodShortName": "GetExpiringEntries(...)", "fileIndex": 0, "line": 795,
    "metrics": [
      { "value": 41, "exceeded": true },
      { "value": 14, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Data.ITeamRepository,TeamsManager.Core.Abstractions.Data.IUserRepository,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.TeamMember>,TeamsManager.Core.Abstractions.Data.ITeamTemplateRepository,TeamsManager.Core.Abstractions.Data.IOperationHistoryRepository,TeamsManager.Core.Abstractions.ICurrentUserService,TeamsManager.Core.Abstractions.Services.Graph.IGraphTeamManagementService,TeamsManager.Core.Abstractions.Services.Graph.IGraphUserManagementService,TeamsManager.Core.Abstractions.Services.Graph.IGraphBulkOperationsService,TeamsManager.Core.Abstractions.Services.Graph.IGraphService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.Services.IAdminNotificationService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.TeamService>,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.SchoolType>,TeamsManager.Core.Abstractions.Data.ISchoolYearRepository,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.Services.Graph.IGraphCacheService,TeamsManager.Core.Abstractions.Services.Cache.ICacheInvalidationService,TeamsManager.Core.Abstractions.Data.IUnitOfWork,TeamsManager.Core.Abstractions.Services.Synchronization.IGraphSynchronizer`2<TeamsManager.Core.Models.Team,TeamsManager.Core.Models.Team>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 63,
    "metrics": [
      { "value": 38, "exceeded": true },
      { "value": 38, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.SchoolYearProcessOrchestrator", "reportPath": "TeamsManager.Application_SchoolYearProcessOrchestrator.html", "methodName": "CreateTeamsForNewSchoolYearAsync()", "methodShortName": "CreateTeamsForNewSchoolYearAsync()", "fileIndex": 0, "line": 59,
    "metrics": [
      { "value": 37, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphConnectionService", "reportPath": "TeamsManager.Core_GraphConnectionService.html", "methodName": "IsTokenValidAsync()", "methodShortName": "IsTokenValidAsync()", "fileIndex": 0, "line": 46,
    "metrics": [
      { "value": 35, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "BulkArchiveTeamsAsync()", "methodShortName": "BulkArchiveTeamsAsync()", "fileIndex": 0, "line": 356,
    "metrics": [
      { "value": 34, "exceeded": true },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphBulkOperationsService", "reportPath": "TeamsManager.Core_GraphBulkOperationsService.html", "methodName": "ExecuteBatchOperationsAsync()", "methodShortName": "ExecuteBatchOperationsAsync()", "fileIndex": 0, "line": 1806,
    "metrics": [
      { "value": 34, "exceeded": true },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserService", "reportPath": "TeamsManager.Core_UserService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Data.IUserRepository,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.Department>,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.UserSchoolType>,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.SchoolType>,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.UserSubject>,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.Subject>,TeamsManager.Core.Abstractions.Data.IOperationHistoryRepository,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.UserService>,TeamsManager.Core.Abstractions.Services.ISubjectService,TeamsManager.Core.Abstractions.Services.Graph.IGraphService,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.Services.Graph.IGraphCacheService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.Services.IAdminNotificationService,TeamsManager.Core.Abstractions.Services.Synchronization.IGraphSynchronizer`2<TeamsManager.Core.Models.User,TeamsManager.Core.Models.Graph.GraphUser>,TeamsManager.Core.Abstractions.Data.IUnitOfWork)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 52,
    "metrics": [
      { "value": 34, "exceeded": true },
      { "value": 34, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "ProcessOnboardingBatch()", "methodShortName": "ProcessOnboardingBatch()", "fileIndex": 0, "line": 606,
    "metrics": [
      { "value": 33, "exceeded": true },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.BulkUserManagementController", "reportPath": "TeamsManager.Api_BulkUserManagementController.html", "methodName": "BulkUserOffboarding()", "methodShortName": "BulkUserOffboarding()", "fileIndex": 0, "line": 108,
    "metrics": [
      { "value": 32, "exceeded": true },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Hubs.NotificationHub", "reportPath": "TeamsManager.Api_NotificationHub.html", "methodName": "OnConnectedAsync()", "methodShortName": "OnConnectedAsync()", "fileIndex": 0, "line": 30,
    "metrics": [
      { "value": 31, "exceeded": true },
      { "value": 30, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Data", "class": "TeamsManager.Data.UnitOfWork.EfUnitOfWork", "reportPath": "TeamsManager.Data_EfUnitOfWork.html", "methodName": "RollbackAsync()", "methodShortName": "RollbackAsync()", "fileIndex": 0, "line": 169,
    "metrics": [
      { "value": 31, "exceeded": true },
      { "value": 8, "exceeded": false },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.API.Controllers.TeamLifecycleController", "reportPath": "TeamsManager.Api_TeamLifecycleController.html", "methodName": "BulkArchiveTeamsWithCleanup()", "methodShortName": "BulkArchiveTeamsWithCleanup()", "fileIndex": 0, "line": 42,
    "metrics": [
      { "value": 28, "exceeded": false },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserContext.CurrentUserService", "reportPath": "TeamsManager.Core_CurrentUserService.html", "methodName": "GetCurrentUserUpn()", "methodShortName": "GetCurrentUserUpn()", "fileIndex": 0, "line": 36,
    "metrics": [
      { "value": 30, "exceeded": false },
      { "value": 28, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.UsersController", "reportPath": "TeamsManager.Api_UsersController.html", "methodName": "CreateUser()", "methodShortName": "CreateUser()", "fileIndex": 0, "line": 156,
    "metrics": [
      { "value": 27, "exceeded": false },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IUserService,TeamsManager.Core.Abstractions.Services.ITeamService,TeamsManager.Core.Abstractions.Services.IDepartmentService,TeamsManager.Core.Abstractions.Services.ISubjectService,TeamsManager.Core.Abstractions.Services.Graph.IGraphBulkOperationsService,TeamsManager.Core.Abstractions.Services.Graph.IGraphUserManagementService,TeamsManager.Core.Abstractions.Services.Graph.IGraphService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.Services.IAdminNotificationService,TeamsManager.Core.Abstractions.Services.Cache.ICacheInvalidationService,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Application.Services.BulkUserManagementOrchestrator>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 47,
    "metrics": [
      { "value": 26, "exceeded": false },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.DepartmentService", "reportPath": "TeamsManager.Core_DepartmentService.html", "methodName": "GetDepartmentByIdAsync()", "methodShortName": "GetDepartmentByIdAsync()", "fileIndex": 0, "line": 61,
    "metrics": [
      { "value": 29, "exceeded": false },
      { "value": 26, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.GraphUser", "reportPath": "TeamsManager.Core_GraphUser.html", "methodName": "GetDetailedInfo()", "methodShortName": "GetDetailedInfo()", "fileIndex": 0, "line": 332,
    "metrics": [
      { "value": 24, "exceeded": false },
      { "value": 24, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.DiagnosticsController", "reportPath": "TeamsManager.Api_DiagnosticsController.html", "methodName": "GenerateRecommendations(TeamsManager.Core.Models.Graph.GraphDiagnosticInfo,TeamsManager.Core.Models.Graph.GraphPermissionInfo)", "methodShortName": "GenerateRecommendations(...)", "fileIndex": 0, "line": 373,
    "metrics": [
      { "value": 26, "exceeded": false },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.TeamLifecycleOrchestrator", "reportPath": "TeamsManager.Application_TeamLifecycleOrchestrator.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.ITeamService,TeamsManager.Core.Abstractions.Services.IUserService,TeamsManager.Core.Abstractions.Services.ISchoolYearService,TeamsManager.Core.Abstractions.Services.Graph.IGraphBulkOperationsService,TeamsManager.Core.Abstractions.Services.Graph.IGraphService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.Services.IAdminNotificationService,TeamsManager.Core.Abstractions.Services.Cache.ICacheInvalidationService,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Application.Services.TeamLifecycleOrchestrator>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 43,
    "metrics": [
      { "value": 22, "exceeded": false },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ChannelService", "reportPath": "TeamsManager.Core_ChannelService.html", "methodName": "CreateTeamChannelAsync()", "methodShortName": "CreateTeamChannelAsync()", "fileIndex": 0, "line": 323,
    "metrics": [
      { "value": 25, "exceeded": false },
      { "value": 22, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.BulkUserManagementController", "reportPath": "TeamsManager.Api_BulkUserManagementController.html", "methodName": "BulkRoleChange()", "methodShortName": "BulkRoleChange()", "fileIndex": 0, "line": 171,
    "metrics": [
      { "value": 22, "exceeded": false },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.BulkUserManagementController", "reportPath": "TeamsManager.Api_BulkUserManagementController.html", "methodName": "BulkTeamMembershipOperation()", "methodShortName": "BulkTeamMembershipOperation()", "fileIndex": 0, "line": 233,
    "metrics": [
      { "value": 29, "exceeded": false },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.BulkUserManagementController", "reportPath": "TeamsManager.Api_BulkUserManagementController.html", "methodName": "BulkUserOnboarding()", "methodShortName": "BulkUserOnboarding()", "fileIndex": 0, "line": 46,
    "metrics": [
      { "value": 20, "exceeded": false },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "BulkUserOffboardingAsync()", "methodShortName": "BulkUserOffboardingAsync()", "fileIndex": 0, "line": 202,
    "metrics": [
      { "value": 20, "exceeded": false },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.GraphChannel", "reportPath": "TeamsManager.Core_GraphChannel.html", "methodName": "ToLocalChannel()", "methodShortName": "ToLocalChannel()", "fileIndex": 0, "line": 123,
    "metrics": [
      { "value": 20, "exceeded": false },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.ChannelService", "reportPath": "TeamsManager.Core_ChannelService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.Graph.IGraphService,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.Channel>,TeamsManager.Core.Abstractions.Data.ITeamRepository,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.ChannelService>,TeamsManager.Core.Abstractions.Services.Graph.IGraphCacheService,TeamsManager.Core.Abstractions.Services.Synchronization.IGraphSynchronizer`2<TeamsManager.Core.Models.Channel,TeamsManager.Core.Models.Graph.GraphChannel>,TeamsManager.Core.Abstractions.Data.IUnitOfWork)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 37,
    "metrics": [
      { "value": 20, "exceeded": false },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolYearService", "reportPath": "TeamsManager.Core_SchoolYearService.html", "methodName": "CreateSchoolYearAsync()", "methodShortName": "CreateSchoolYearAsync()", "fileIndex": 0, "line": 290,
    "metrics": [
      { "value": 21, "exceeded": false },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SubjectService", "reportPath": "TeamsManager.Core_SubjectService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Data.ISubjectRepository,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.SchoolType>,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.UserSubject>,TeamsManager.Core.Abstractions.Data.IUserRepository,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,TeamsManager.Core.Abstractions.Services.Graph.IGraphCacheService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.SubjectService>,Microsoft.Extensions.Caching.Memory.IMemoryCache)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 39,
    "metrics": [
      { "value": 20, "exceeded": false },
      { "value": 20, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.BulkUserManagementOrchestrator", "reportPath": "TeamsManager.Application_BulkUserManagementOrchestrator.html", "methodName": "BulkUserOnboardingAsync()", "methodShortName": "BulkUserOnboardingAsync()", "fileIndex": 0, "line": 85,
    "metrics": [
      { "value": 18, "exceeded": false },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Auth.TokenManager", "reportPath": "TeamsManager.Core_TokenManager.html", "methodName": "GetValidAccessTokenAsync()", "methodShortName": "GetValidAccessTokenAsync()", "fileIndex": 0, "line": 35,
    "metrics": [
      { "value": 23, "exceeded": false },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.Graph.GraphCacheService", "reportPath": "TeamsManager.Core_GraphCacheService.html", "methodName": "UpdateETag(System.String,System.String)", "methodShortName": "UpdateETag(...)", "fileIndex": 0, "line": 1203,
    "metrics": [
      { "value": 27, "exceeded": false },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolYearService", "reportPath": "TeamsManager.Core_SchoolYearService.html", "methodName": "GetSchoolYearByIdAsync()", "methodShortName": "GetSchoolYearByIdAsync()", "fileIndex": 0, "line": 73,
    "metrics": [
      { "value": 18, "exceeded": false },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamService", "reportPath": "TeamsManager.Core_TeamService.html", "methodName": "<CreateTeamAsync()", "methodShortName": "<CreateTeamAsync()", "fileIndex": 0, "line": 451,
    "metrics": [
      { "value": 20, "exceeded": false },
      { "value": 18, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.API.Controllers.SchoolYearProcessController", "reportPath": "TeamsManager.Api_SchoolYearProcessController.html", "methodName": "CreateTeamsForNewSchoolYear()", "methodShortName": "CreateTeamsForNewSchoolYear()", "fileIndex": 0, "line": 46,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Api", "class": "TeamsManager.Api.Controllers.TestAuthController", "reportPath": "TeamsManager.Api_TestAuthController.html", "methodName": "WhoAmI()", "methodShortName": "WhoAmI()", "fileIndex": 0, "line": 30,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.ReportingOrchestrator", "reportPath": "TeamsManager.Application_ReportingOrchestrator.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.Services.ISchoolYearService,TeamsManager.Core.Abstractions.Services.ITeamService,TeamsManager.Core.Abstractions.Services.IUserService,TeamsManager.Core.Abstractions.Services.IDepartmentService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Application.Services.ReportingOrchestrator>)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 41,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Application", "class": "TeamsManager.Application.Services.ReportingOrchestrator", "reportPath": "TeamsManager.Application_ReportingOrchestrator.html", "methodName": "GenerateSchoolYearReportAsync()", "methodShortName": "GenerateSchoolYearReportAsync()", "fileIndex": 0, "line": 66,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.ApplicationSetting", "reportPath": "TeamsManager.Core_ApplicationSetting.html", "methodName": "IsValid()", "methodShortName": "IsValid()", "fileIndex": 0, "line": 183,
    "metrics": [
      { "value": 17, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Department", "reportPath": "TeamsManager.Core_Department.html", "methodName": "IsChildOf(System.String)", "methodShortName": "IsChildOf(...)", "fileIndex": 0, "line": 294,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.Graph.GraphServiceConfiguration", "reportPath": "TeamsManager.Core_GraphServiceConfiguration.html", "methodName": "IsValid()", "methodShortName": "IsValid()", "fileIndex": 0, "line": 72,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.SchoolYear", "reportPath": "TeamsManager.Core_SchoolYear.html", "methodName": "get_CurrentSemester()", "methodShortName": "get_CurrentSemester()", "fileIndex": 0, "line": 125,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.TeamTemplate", "reportPath": "TeamsManager.Core_TeamTemplate.html", "methodName": "GenerateTeamName(System.Collections.Generic.Dictionary`2<System.String,System.String>,System.String)", "methodShortName": "GenerateTeamName(...)", "fileIndex": 0, "line": 193,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Models.TeamTemplate", "reportPath": "TeamsManager.Core_TeamTemplate.html", "methodName": "ValidateTemplate()", "methodShortName": "ValidateTemplate()", "fileIndex": 0, "line": 233,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.SchoolYearService", "reportPath": "TeamsManager.Core_SchoolYearService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Data.ISchoolYearRepository,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.SchoolYearService>,TeamsManager.Core.Abstractions.Data.ITeamRepository,Microsoft.Extensions.Caching.Memory.IMemoryCache,TeamsManager.Core.Abstractions.Services.Graph.IGraphCacheService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 37,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.TeamTemplateService", "reportPath": "TeamsManager.Core_TeamTemplateService.html", "methodName": ".ctor(TeamsManager.Core.Abstractions.Data.ITeamTemplateRepository,TeamsManager.Core.Abstractions.Data.IGenericRepository`1<TeamsManager.Core.Models.SchoolType>,TeamsManager.Core.Abstractions.Services.IOperationHistoryService,TeamsManager.Core.Abstractions.Services.INotificationService,TeamsManager.Core.Abstractions.ICurrentUserService,Microsoft.Extensions.Logging.ILogger`1<TeamsManager.Core.Services.TeamTemplateService>,Microsoft.Extensions.Caching.Memory.IMemoryCache,TeamsManager.Core.Abstractions.Services.Graph.IGraphCacheService)", "methodShortName": ".ctor(...)", "fileIndex": 0, "line": 34,
    "metrics": [
      { "value": 16, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Services.UserContext.CurrentUserService", "reportPath": "TeamsManager.Core_CurrentUserService.html", "methodName": "GetCurrentUserId()", "methodShortName": "GetCurrentUserId()", "fileIndex": 0, "line": 98,
    "metrics": [
      { "value": 17, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
  {
    "assembly": "TeamsManager.Core", "class": "TeamsManager.Core.Validation.OrganizationalUnitValidator", "reportPath": "TeamsManager.Core_OrganizationalUnitValidator.html", "methodName": "ValidateBasicFields(TeamsManager.Core.Models.OrganizationalUnit,TeamsManager.Core.Validation.ValidationResult)", "methodShortName": "ValidateBasicFields(...)", "fileIndex": 0, "line": 202,
    "metrics": [
      { "value": 28, "exceeded": false },
      { "value": 16, "exceeded": true },
    ]},
];

var branchCoverageAvailable = true;
var methodCoverageAvailable = false;
var applyMaximumGroupingLevel = false;
var maximumDecimalPlacesForCoverageQuotas = 1;


var translations = {
'top': 'Top:',
'all': 'All',
'assembly': 'Assembly',
'class': 'Class',
'method': 'Method',
'lineCoverage': 'Line coverage',
'noGrouping': 'No grouping',
'byAssembly': 'By assembly',
'byNamespace': 'By namespace, Level:',
'all': 'All',
'collapseAll': 'Collapse all',
'expandAll': 'Expand all',
'grouping': 'Grouping:',
'filter': 'Filter:',
'name': 'Name',
'covered': 'Covered',
'uncovered': 'Uncovered',
'coverable': 'Coverable',
'total': 'Total',
'coverage': 'Line coverage',
'branchCoverage': 'Branch coverage',
'methodCoverage': 'Method coverage',
'fullMethodCoverage': 'Full method coverage',
'percentage': 'Percentage',
'history': 'Coverage history',
'compareHistory': 'Compare with:',
'date': 'Date',
'allChanges': 'All changes',
'selectCoverageTypes': 'Select coverage types',
'selectCoverageTypesAndMetrics': 'Select coverage types & metrics',
'coverageTypes': 'Coverage types',
'metrics': 'Metrics',
'methodCoverageProVersion': 'Feature is only available for sponsors',
'lineCoverageIncreaseOnly': 'Line coverage: Increase only',
'lineCoverageDecreaseOnly': 'Line coverage: Decrease only',
'branchCoverageIncreaseOnly': 'Branch coverage: Increase only',
'branchCoverageDecreaseOnly': 'Branch coverage: Decrease only',
'methodCoverageIncreaseOnly': 'Method coverage: Increase only',
'methodCoverageDecreaseOnly': 'Method coverage: Decrease only',
'fullMethodCoverageIncreaseOnly': 'Full method coverage: Increase only',
'fullMethodCoverageDecreaseOnly': 'Full method coverage: Decrease only'
};


(()=>{"use strict";var e,_={},p={};function n(e){var a=p[e];if(void 0!==a)return a.exports;var r=p[e]={exports:{}};return _[e](r,r.exports,n),r.exports}n.m=_,e=[],n.O=(a,r,u,l)=>{if(!r){var o=1/0;for(f=0;f<e.length;f++){for(var[r,u,l]=e[f],v=!0,t=0;t<r.length;t++)(!1&l||o>=l)&&Object.keys(n.O).every(h=>n.O[h](r[t]))?r.splice(t--,1):(v=!1,l<o&&(o=l));if(v){e.splice(f--,1);var c=u();void 0!==c&&(a=c)}}return a}l=l||0;for(var f=e.length;f>0&&e[f-1][2]>l;f--)e[f]=e[f-1];e[f]=[r,u,l]},n.n=e=>{var a=e&&e.__esModule?()=>e.default:()=>e;return n.d(a,{a}),a},n.d=(e,a)=>{for(var r in a)n.o(a,r)&&!n.o(e,r)&&Object.defineProperty(e,r,{enumerable:!0,get:a[r]})},n.o=(e,a)=>Object.prototype.hasOwnProperty.call(e,a),(()=>{var e={121:0};n.O.j=u=>0===e[u];var a=(u,l)=>{var t,c,[f,o,v]=l,s=0;if(f.some(d=>0!==e[d])){for(t in o)n.o(o,t)&&(n.m[t]=o[t]);if(v)var b=v(n)}for(u&&u(l);s<f.length;s++)n.o(e,c=f[s])&&e[c]&&e[c][0](),e[c]=0;return n.O(b)},r=self.webpackChunkcoverage_app=self.webpackChunkcoverage_app||[];r.forEach(a.bind(null,0)),r.push=a.bind(null,r.push.bind(r))})()})();

"use strict";(self.webpackChunkcoverage_app=self.webpackChunkcoverage_app||[]).push([[461],{50:(te,Q,ve)=>{ve(935)},935:()=>{const te=globalThis;function Q(e){return(te.__Zone_symbol_prefix||"__zone_symbol__")+e}const Te=Object.getOwnPropertyDescriptor,Le=Object.defineProperty,Ie=Object.getPrototypeOf,_t=Object.create,Et=Array.prototype.slice,Me="addEventListener",Ze="removeEventListener",Ae=Q(Me),je=Q(Ze),ae="true",le="false",Pe=Q("");function He(e,r){return Zone.current.wrap(e,r)}function xe(e,r,c,t,i){return Zone.current.scheduleMacroTask(e,r,c,t,i)}const j=Q,Ce=typeof window<"u",ge=Ce?window:void 0,$=Ce&&ge||globalThis;function Ve(e,r){for(let c=e.length-1;c>=0;c--)"function"==typeof e[c]&&(e[c]=He(e[c],r+"_"+c));return e}function We(e){return!e||!1!==e.writable&&!("function"==typeof e.get&&typeof e.set>"u")}const qe=typeof WorkerGlobalScope<"u"&&self instanceof WorkerGlobalScope,De=!("nw"in $)&&typeof $.process<"u"&&"[object process]"===$.process.toString(),Ge=!De&&!qe&&!(!Ce||!ge.HTMLElement),Xe=typeof $.process<"u"&&"[object process]"===$.process.toString()&&!qe&&!(!Ce||!ge.HTMLElement),Se={},pt=j("enable_beforeunload"),Ye=function(e){if(!(e=e||$.event))return;let r=Se[e.type];r||(r=Se[e.type]=j("ON_PROPERTY"+e.type));const c=this||e.target||$,t=c[r];let i;return Ge&&c===ge&&"error"===e.type?(i=t&&t.call(this,e.message,e.filename,e.lineno,e.colno,e.error),!0===i&&e.preventDefault()):(i=t&&t.apply(this,arguments),"beforeunload"===e.type&&$[pt]&&"string"==typeof i?e.returnValue=i:null!=i&&!i&&e.preventDefault()),i};function $e(e,r,c){let t=Te(e,r);if(!t&&c&&Te(c,r)&&(t={enumerable:!0,configurable:!0}),!t||!t.configurable)return;const i=j("on"+r+"patched");if(e.hasOwnProperty(i)&&e[i])return;delete t.writable,delete t.value;const u=t.get,E=t.set,T=r.slice(2);let y=Se[T];y||(y=Se[T]=j("ON_PROPERTY"+T)),t.set=function(D){let d=this;!d&&e===$&&(d=$),d&&("function"==typeof d[y]&&d.removeEventListener(T,Ye),E&&E.call(d,null),d[y]=D,"function"==typeof D&&d.addEventListener(T,Ye,!1))},t.get=function(){let D=this;if(!D&&e===$&&(D=$),!D)return null;const d=D[y];if(d)return d;if(u){let w=u.call(this);if(w)return t.set.call(this,w),"function"==typeof D.removeAttribute&&D.removeAttribute(r),w}return null},Le(e,r,t),e[i]=!0}function Ke(e,r,c){if(r)for(let t=0;t<r.length;t++)$e(e,"on"+r[t],c);else{const t=[];for(const i in e)"on"==i.slice(0,2)&&t.push(i);for(let i=0;i<t.length;i++)$e(e,t[i],c)}}const re=j("originalInstance");function we(e){const r=$[e];if(!r)return;$[j(e)]=r,$[e]=function(){const i=Ve(arguments,e);switch(i.length){case 0:this[re]=new r;break;case 1:this[re]=new r(i[0]);break;case 2:this[re]=new r(i[0],i[1]);break;case 3:this[re]=new r(i[0],i[1],i[2]);break;case 4:this[re]=new r(i[0],i[1],i[2],i[3]);break;default:throw new Error("Arg list too long.")}},fe($[e],r);const c=new r(function(){});let t;for(t in c)"XMLHttpRequest"===e&&"responseBlob"===t||function(i){"function"==typeof c[i]?$[e].prototype[i]=function(){return this[re][i].apply(this[re],arguments)}:Le($[e].prototype,i,{set:function(u){"function"==typeof u?(this[re][i]=He(u,e+"."+i),fe(this[re][i],u)):this[re][i]=u},get:function(){return this[re][i]}})}(t);for(t in r)"prototype"!==t&&r.hasOwnProperty(t)&&($[e][t]=r[t])}function ue(e,r,c){let t=e;for(;t&&!t.hasOwnProperty(r);)t=Ie(t);!t&&e[r]&&(t=e);const i=j(r);let u=null;if(t&&(!(u=t[i])||!t.hasOwnProperty(i))&&(u=t[i]=t[r],We(t&&Te(t,r)))){const T=c(u,i,r);t[r]=function(){return T(this,arguments)},fe(t[r],u)}return u}function yt(e,r,c){let t=null;function i(u){const E=u.data;return E.args[E.cbIdx]=function(){u.invoke.apply(this,arguments)},t.apply(E.target,E.args),u}t=ue(e,r,u=>function(E,T){const y=c(E,T);return y.cbIdx>=0&&"function"==typeof T[y.cbIdx]?xe(y.name,T[y.cbIdx],y,i):u.apply(E,T)})}function fe(e,r){e[j("OriginalDelegate")]=r}let Je=!1,Be=!1;function kt(){if(Je)return Be;Je=!0;try{const e=ge.navigator.userAgent;(-1!==e.indexOf("MSIE ")||-1!==e.indexOf("Trident/")||-1!==e.indexOf("Edge/"))&&(Be=!0)}catch{}return Be}function Qe(e){return"function"==typeof e}function et(e){return"number"==typeof e}let pe=!1;if(typeof window<"u")try{const e=Object.defineProperty({},"passive",{get:function(){pe=!0}});window.addEventListener("test",e,e),window.removeEventListener("test",e,e)}catch{pe=!1}const vt={useG:!0},ne={},tt={},nt=new RegExp("^"+Pe+"(\\w+)(true|false)$"),rt=j("propagationStopped");function ot(e,r){const c=(r?r(e):e)+le,t=(r?r(e):e)+ae,i=Pe+c,u=Pe+t;ne[e]={},ne[e][le]=i,ne[e][ae]=u}function bt(e,r,c,t){const i=t&&t.add||Me,u=t&&t.rm||Ze,E=t&&t.listeners||"eventListeners",T=t&&t.rmAll||"removeAllListeners",y=j(i),D="."+i+":",d="prependListener",w="."+d+":",Z=function(k,h,H){if(k.isRemoved)return;const V=k.callback;let Y;"object"==typeof V&&V.handleEvent&&(k.callback=g=>V.handleEvent(g),k.originalDelegate=V);try{k.invoke(k,h,[H])}catch(g){Y=g}const G=k.options;return G&&"object"==typeof G&&G.once&&h[u].call(h,H.type,k.originalDelegate?k.originalDelegate:k.callback,G),Y};function x(k,h,H){if(!(h=h||e.event))return;const V=k||h.target||e,Y=V[ne[h.type][H?ae:le]];if(Y){const G=[];if(1===Y.length){const g=Z(Y[0],V,h);g&&G.push(g)}else{const g=Y.slice();for(let z=0;z<g.length&&(!h||!0!==h[rt]);z++){const O=Z(g[z],V,h);O&&G.push(O)}}if(1===G.length)throw G[0];for(let g=0;g<G.length;g++){const z=G[g];r.nativeScheduleMicroTask(()=>{throw z})}}}const U=function(k){return x(this,k,!1)},K=function(k){return x(this,k,!0)};function J(k,h){if(!k)return!1;let H=!0;h&&void 0!==h.useG&&(H=h.useG);const V=h&&h.vh;let Y=!0;h&&void 0!==h.chkDup&&(Y=h.chkDup);let G=!1;h&&void 0!==h.rt&&(G=h.rt);let g=k;for(;g&&!g.hasOwnProperty(i);)g=Ie(g);if(!g&&k[i]&&(g=k),!g||g[y])return!1;const z=h&&h.eventNameToString,O={},R=g[y]=g[i],b=g[j(u)]=g[u],S=g[j(E)]=g[E],ee=g[j(T)]=g[T];let W;h&&h.prepend&&(W=g[j(h.prepend)]=g[h.prepend]);const q=H?function(s){if(!O.isExisting)return R.call(O.target,O.eventName,O.capture?K:U,O.options)}:function(s){return R.call(O.target,O.eventName,s.invoke,O.options)},A=H?function(s){if(!s.isRemoved){const l=ne[s.eventName];let v;l&&(v=l[s.capture?ae:le]);const C=v&&s.target[v];if(C)for(let p=0;p<C.length;p++)if(C[p]===s){C.splice(p,1),s.isRemoved=!0,s.removeAbortListener&&(s.removeAbortListener(),s.removeAbortListener=null),0===C.length&&(s.allRemoved=!0,s.target[v]=null);break}}if(s.allRemoved)return b.call(s.target,s.eventName,s.capture?K:U,s.options)}:function(s){return b.call(s.target,s.eventName,s.invoke,s.options)},he=h&&h.diff?h.diff:function(s,l){const v=typeof l;return"function"===v&&s.callback===l||"object"===v&&s.originalDelegate===l},de=Zone[j("UNPATCHED_EVENTS")],oe=e[j("PASSIVE_EVENTS")],a=function(s,l,v,C,p=!1,L=!1){return function(){const I=this||e;let M=arguments[0];h&&h.transferEventName&&(M=h.transferEventName(M));let B=arguments[1];if(!B)return s.apply(this,arguments);if(De&&"uncaughtException"===M)return s.apply(this,arguments);let F=!1;if("function"!=typeof B){if(!B.handleEvent)return s.apply(this,arguments);F=!0}if(V&&!V(s,B,I,arguments))return;const Ee=pe&&!!oe&&-1!==oe.indexOf(M),ie=function f(s){if("object"==typeof s&&null!==s){const l={...s};return s.signal&&(l.signal=s.signal),l}return s}(function N(s,l){return!pe&&"object"==typeof s&&s?!!s.capture:pe&&l?"boolean"==typeof s?{capture:s,passive:!0}:s?"object"==typeof s&&!1!==s.passive?{...s,passive:!0}:s:{passive:!0}:s}(arguments[2],Ee)),me=ie?.signal;if(me?.aborted)return;if(de)for(let ce=0;ce<de.length;ce++)if(M===de[ce])return Ee?s.call(I,M,B,ie):s.apply(this,arguments);const Ue=!!ie&&("boolean"==typeof ie||ie.capture),lt=!(!ie||"object"!=typeof ie)&&ie.once,At=Zone.current;let ze=ne[M];ze||(ot(M,z),ze=ne[M]);const ut=ze[Ue?ae:le];let Ne,ke=I[ut],ft=!1;if(ke){if(ft=!0,Y)for(let ce=0;ce<ke.length;ce++)if(he(ke[ce],B))return}else ke=I[ut]=[];const ht=I.constructor.name,dt=tt[ht];dt&&(Ne=dt[M]),Ne||(Ne=ht+l+(z?z(M):M)),O.options=ie,lt&&(O.options.once=!1),O.target=I,O.capture=Ue,O.eventName=M,O.isExisting=ft;const Re=H?vt:void 0;Re&&(Re.taskData=O),me&&(O.options.signal=void 0);const se=At.scheduleEventTask(Ne,B,Re,v,C);if(me){O.options.signal=me;const ce=()=>se.zone.cancelTask(se);s.call(me,"abort",ce,{once:!0}),se.removeAbortListener=()=>me.removeEventListener("abort",ce)}return O.target=null,Re&&(Re.taskData=null),lt&&(O.options.once=!0),!pe&&"boolean"==typeof se.options||(se.options=ie),se.target=I,se.capture=Ue,se.eventName=M,F&&(se.originalDelegate=B),L?ke.unshift(se):ke.push(se),p?I:void 0}};return g[i]=a(R,D,q,A,G),W&&(g[d]=a(W,w,function(s){return W.call(O.target,O.eventName,s.invoke,O.options)},A,G,!0)),g[u]=function(){const s=this||e;let l=arguments[0];h&&h.transferEventName&&(l=h.transferEventName(l));const v=arguments[2],C=!!v&&("boolean"==typeof v||v.capture),p=arguments[1];if(!p)return b.apply(this,arguments);if(V&&!V(b,p,s,arguments))return;const L=ne[l];let I;L&&(I=L[C?ae:le]);const M=I&&s[I];if(M)for(let B=0;B<M.length;B++){const F=M[B];if(he(F,p))return M.splice(B,1),F.isRemoved=!0,0!==M.length||(F.allRemoved=!0,s[I]=null,C||"string"!=typeof l)||(s[Pe+"ON_PROPERTY"+l]=null),F.zone.cancelTask(F),G?s:void 0}return b.apply(this,arguments)},g[E]=function(){const s=this||e;let l=arguments[0];h&&h.transferEventName&&(l=h.transferEventName(l));const v=[],C=st(s,z?z(l):l);for(let p=0;p<C.length;p++){const L=C[p];v.push(L.originalDelegate?L.originalDelegate:L.callback)}return v},g[T]=function(){const s=this||e;let l=arguments[0];if(l){h&&h.transferEventName&&(l=h.transferEventName(l));const v=ne[l];if(v){const L=s[v[le]],I=s[v[ae]];if(L){const M=L.slice();for(let B=0;B<M.length;B++){const F=M[B];this[u].call(this,l,F.originalDelegate?F.originalDelegate:F.callback,F.options)}}if(I){const M=I.slice();for(let B=0;B<M.length;B++){const F=M[B];this[u].call(this,l,F.originalDelegate?F.originalDelegate:F.callback,F.options)}}}}else{const v=Object.keys(s);for(let C=0;C<v.length;C++){const L=nt.exec(v[C]);let I=L&&L[1];I&&"removeListener"!==I&&this[T].call(this,I)}this[T].call(this,"removeListener")}if(G)return this},fe(g[i],R),fe(g[u],b),ee&&fe(g[T],ee),S&&fe(g[E],S),!0}let X=[];for(let k=0;k<c.length;k++)X[k]=J(c[k],t);return X}function st(e,r){if(!r){const u=[];for(let E in e){const T=nt.exec(E);let y=T&&T[1];if(y&&(!r||y===r)){const D=e[E];if(D)for(let d=0;d<D.length;d++)u.push(D[d])}}return u}let c=ne[r];c||(ot(r),c=ne[r]);const t=e[c[le]],i=e[c[ae]];return t?i?t.concat(i):t.slice():i?i.slice():[]}function Pt(e,r){const c=e.Event;c&&c.prototype&&r.patchMethod(c.prototype,"stopImmediatePropagation",t=>function(i,u){i[rt]=!0,t&&t.apply(i,u)})}const Oe=j("zoneTask");function ye(e,r,c,t){let i=null,u=null;c+=t;const E={};function T(D){const d=D.data;d.args[0]=function(){return D.invoke.apply(this,arguments)};const w=i.apply(e,d.args);return et(w)?d.handleId=w:(d.handle=w,d.isRefreshable=Qe(w.refresh)),D}function y(D){const{handle:d,handleId:w}=D.data;return u.call(e,d??w)}i=ue(e,r+=t,D=>function(d,w){if(Qe(w[0])){const Z={isRefreshable:!1,isPeriodic:"Interval"===t,delay:"Timeout"===t||"Interval"===t?w[1]||0:void 0,args:w},x=w[0];w[0]=function(){try{return x.apply(this,arguments)}finally{const{handle:H,handleId:V,isPeriodic:Y,isRefreshable:G}=Z;!Y&&!G&&(V?delete E[V]:H&&(H[Oe]=null))}};const U=xe(r,w[0],Z,T,y);if(!U)return U;const{handleId:K,handle:J,isRefreshable:X,isPeriodic:k}=U.data;if(K)E[K]=U;else if(J&&(J[Oe]=U,X&&!k)){const h=J.refresh;J.refresh=function(){const{zone:H,state:V}=U;return"notScheduled"===V?(U._state="scheduled",H._updateTaskCount(U,1)):"running"===V&&(U._state="scheduling"),h.call(this)}}return J??K??U}return D.apply(e,w)}),u=ue(e,c,D=>function(d,w){const Z=w[0];let x;et(Z)?(x=E[Z],delete E[Z]):(x=Z?.[Oe],x?Z[Oe]=null:x=Z),x?.type?x.cancelFn&&x.zone.cancelTask(x):D.apply(e,w)})}function it(e,r,c){if(!c||0===c.length)return r;const t=c.filter(u=>u.target===e);if(!t||0===t.length)return r;const i=t[0].ignoreProperties;return r.filter(u=>-1===i.indexOf(u))}function ct(e,r,c,t){e&&Ke(e,it(e,r,c),t)}function Fe(e){return Object.getOwnPropertyNames(e).filter(r=>r.startsWith("on")&&r.length>2).map(r=>r.substring(2))}function It(e,r,c,t,i){const u=Zone.__symbol__(t);if(r[u])return;const E=r[u]=r[t];r[t]=function(T,y,D){return y&&y.prototype&&i.forEach(function(d){const w=`${c}.${t}::`+d,Z=y.prototype;try{if(Z.hasOwnProperty(d)){const x=e.ObjectGetOwnPropertyDescriptor(Z,d);x&&x.value?(x.value=e.wrapWithCurrentZone(x.value,w),e._redefineProperty(y.prototype,d,x)):Z[d]&&(Z[d]=e.wrapWithCurrentZone(Z[d],w))}else Z[d]&&(Z[d]=e.wrapWithCurrentZone(Z[d],w))}catch{}}),E.call(r,T,y,D)},e.attachOriginToPatched(r[t],E)}const at=function be(){const e=globalThis,r=!0===e[Q("forceDuplicateZoneCheck")];if(e.Zone&&(r||"function"!=typeof e.Zone.__symbol__))throw new Error("Zone already loaded.");return e.Zone??=function ve(){const e=te.performance;function r(N){e&&e.mark&&e.mark(N)}function c(N,_){e&&e.measure&&e.measure(N,_)}r("Zone");let t=(()=>{class N{static#e=this.__symbol__=Q;static assertZonePatched(){if(te.Promise!==O.ZoneAwarePromise)throw new Error("Zone.js has detected that ZoneAwarePromise `(window|global).Promise` has been overwritten.\nMost likely cause is that a Promise polyfill has been loaded after Zone.js (Polyfilling Promise api is not necessary when zone.js is loaded. If you must load one, do so before loading zone.js.)")}static get root(){let n=N.current;for(;n.parent;)n=n.parent;return n}static get current(){return b.zone}static get currentTask(){return S}static __load_patch(n,o,m=!1){if(O.hasOwnProperty(n)){const P=!0===te[Q("forceDuplicateZoneCheck")];if(!m&&P)throw Error("Already loaded patch: "+n)}else if(!te["__Zone_disable_"+n]){const P="Zone:"+n;r(P),O[n]=o(te,N,R),c(P,P)}}get parent(){return this._parent}get name(){return this._name}constructor(n,o){this._parent=n,this._name=o?o.name||"unnamed":"<root>",this._properties=o&&o.properties||{},this._zoneDelegate=new u(this,this._parent&&this._parent._zoneDelegate,o)}get(n){const o=this.getZoneWith(n);if(o)return o._properties[n]}getZoneWith(n){let o=this;for(;o;){if(o._properties.hasOwnProperty(n))return o;o=o._parent}return null}fork(n){if(!n)throw new Error("ZoneSpec required!");return this._zoneDelegate.fork(this,n)}wrap(n,o){if("function"!=typeof n)throw new Error("Expecting function got: "+n);const m=this._zoneDelegate.intercept(this,n,o),P=this;return function(){return P.runGuarded(m,this,arguments,o)}}run(n,o,m,P){b={parent:b,zone:this};try{return this._zoneDelegate.invoke(this,n,o,m,P)}finally{b=b.parent}}runGuarded(n,o=null,m,P){b={parent:b,zone:this};try{try{return this._zoneDelegate.invoke(this,n,o,m,P)}catch(q){if(this._zoneDelegate.handleError(this,q))throw q}}finally{b=b.parent}}runTask(n,o,m){if(n.zone!=this)throw new Error("A task can only be run in the zone of creation! (Creation: "+(n.zone||J).name+"; Execution: "+this.name+")");const P=n,{type:q,data:{isPeriodic:A=!1,isRefreshable:_e=!1}={}}=n;if(n.state===X&&(q===z||q===g))return;const he=n.state!=H;he&&P._transitionTo(H,h);const de=S;S=P,b={parent:b,zone:this};try{q==g&&n.data&&!A&&!_e&&(n.cancelFn=void 0);try{return this._zoneDelegate.invokeTask(this,P,o,m)}catch(oe){if(this._zoneDelegate.handleError(this,oe))throw oe}}finally{const oe=n.state;if(oe!==X&&oe!==Y)if(q==z||A||_e&&oe===k)he&&P._transitionTo(h,H,k);else{const f=P._zoneDelegates;this._updateTaskCount(P,-1),he&&P._transitionTo(X,H,X),_e&&(P._zoneDelegates=f)}b=b.parent,S=de}}scheduleTask(n){if(n.zone&&n.zone!==this){let m=this;for(;m;){if(m===n.zone)throw Error(`can not reschedule task to ${this.name} which is descendants of the original zone ${n.zone.name}`);m=m.parent}}n._transitionTo(k,X);const o=[];n._zoneDelegates=o,n._zone=this;try{n=this._zoneDelegate.scheduleTask(this,n)}catch(m){throw n._transitionTo(Y,k,X),this._zoneDelegate.handleError(this,m),m}return n._zoneDelegates===o&&this._updateTaskCount(n,1),n.state==k&&n._transitionTo(h,k),n}scheduleMicroTask(n,o,m,P){return this.scheduleTask(new E(G,n,o,m,P,void 0))}scheduleMacroTask(n,o,m,P,q){return this.scheduleTask(new E(g,n,o,m,P,q))}scheduleEventTask(n,o,m,P,q){return this.scheduleTask(new E(z,n,o,m,P,q))}cancelTask(n){if(n.zone!=this)throw new Error("A task can only be cancelled in the zone of creation! (Creation: "+(n.zone||J).name+"; Execution: "+this.name+")");if(n.state===h||n.state===H){n._transitionTo(V,h,H);try{this._zoneDelegate.cancelTask(this,n)}catch(o){throw n._transitionTo(Y,V),this._zoneDelegate.handleError(this,o),o}return this._updateTaskCount(n,-1),n._transitionTo(X,V),n.runCount=-1,n}}_updateTaskCount(n,o){const m=n._zoneDelegates;-1==o&&(n._zoneDelegates=null);for(let P=0;P<m.length;P++)m[P]._updateTaskCount(n.type,o)}}return N})();const i={name:"",onHasTask:(N,_,n,o)=>N.hasTask(n,o),onScheduleTask:(N,_,n,o)=>N.scheduleTask(n,o),onInvokeTask:(N,_,n,o,m,P)=>N.invokeTask(n,o,m,P),onCancelTask:(N,_,n,o)=>N.cancelTask(n,o)};class u{get zone(){return this._zone}constructor(_,n,o){this._taskCounts={microTask:0,macroTask:0,eventTask:0},this._zone=_,this._parentDelegate=n,this._forkZS=o&&(o&&o.onFork?o:n._forkZS),this._forkDlgt=o&&(o.onFork?n:n._forkDlgt),this._forkCurrZone=o&&(o.onFork?this._zone:n._forkCurrZone),this._interceptZS=o&&(o.onIntercept?o:n._interceptZS),this._interceptDlgt=o&&(o.onIntercept?n:n._interceptDlgt),this._interceptCurrZone=o&&(o.onIntercept?this._zone:n._interceptCurrZone),this._invokeZS=o&&(o.onInvoke?o:n._invokeZS),this._invokeDlgt=o&&(o.onInvoke?n:n._invokeDlgt),this._invokeCurrZone=o&&(o.onInvoke?this._zone:n._invokeCurrZone),this._handleErrorZS=o&&(o.onHandleError?o:n._handleErrorZS),this._handleErrorDlgt=o&&(o.onHandleError?n:n._handleErrorDlgt),this._handleErrorCurrZone=o&&(o.onHandleError?this._zone:n._handleErrorCurrZone),this._scheduleTaskZS=o&&(o.onScheduleTask?o:n._scheduleTaskZS),this._scheduleTaskDlgt=o&&(o.onScheduleTask?n:n._scheduleTaskDlgt),this._scheduleTaskCurrZone=o&&(o.onScheduleTask?this._zone:n._scheduleTaskCurrZone),this._invokeTaskZS=o&&(o.onInvokeTask?o:n._invokeTaskZS),this._invokeTaskDlgt=o&&(o.onInvokeTask?n:n._invokeTaskDlgt),this._invokeTaskCurrZone=o&&(o.onInvokeTask?this._zone:n._invokeTaskCurrZone),this._cancelTaskZS=o&&(o.onCancelTask?o:n._cancelTaskZS),this._cancelTaskDlgt=o&&(o.onCancelTask?n:n._cancelTaskDlgt),this._cancelTaskCurrZone=o&&(o.onCancelTask?this._zone:n._cancelTaskCurrZone),this._hasTaskZS=null,this._hasTaskDlgt=null,this._hasTaskDlgtOwner=null,this._hasTaskCurrZone=null;const m=o&&o.onHasTask;(m||n&&n._hasTaskZS)&&(this._hasTaskZS=m?o:i,this._hasTaskDlgt=n,this._hasTaskDlgtOwner=this,this._hasTaskCurrZone=this._zone,o.onScheduleTask||(this._scheduleTaskZS=i,this._scheduleTaskDlgt=n,this._scheduleTaskCurrZone=this._zone),o.onInvokeTask||(this._invokeTaskZS=i,this._invokeTaskDlgt=n,this._invokeTaskCurrZone=this._zone),o.onCancelTask||(this._cancelTaskZS=i,this._cancelTaskDlgt=n,this._cancelTaskCurrZone=this._zone))}fork(_,n){return this._forkZS?this._forkZS.onFork(this._forkDlgt,this.zone,_,n):new t(_,n)}intercept(_,n,o){return this._interceptZS?this._interceptZS.onIntercept(this._interceptDlgt,this._interceptCurrZone,_,n,o):n}invoke(_,n,o,m,P){return this._invokeZS?this._invokeZS.onInvoke(this._invokeDlgt,this._invokeCurrZone,_,n,o,m,P):n.apply(o,m)}handleError(_,n){return!this._handleErrorZS||this._handleErrorZS.onHandleError(this._handleErrorDlgt,this._handleErrorCurrZone,_,n)}scheduleTask(_,n){let o=n;if(this._scheduleTaskZS)this._hasTaskZS&&o._zoneDelegates.push(this._hasTaskDlgtOwner),o=this._scheduleTaskZS.onScheduleTask(this._scheduleTaskDlgt,this._scheduleTaskCurrZone,_,n),o||(o=n);else if(n.scheduleFn)n.scheduleFn(n);else{if(n.type!=G)throw new Error("Task is missing scheduleFn.");U(n)}return o}invokeTask(_,n,o,m){return this._invokeTaskZS?this._invokeTaskZS.onInvokeTask(this._invokeTaskDlgt,this._invokeTaskCurrZone,_,n,o,m):n.callback.apply(o,m)}cancelTask(_,n){let o;if(this._cancelTaskZS)o=this._cancelTaskZS.onCancelTask(this._cancelTaskDlgt,this._cancelTaskCurrZone,_,n);else{if(!n.cancelFn)throw Error("Task is not cancelable");o=n.cancelFn(n)}return o}hasTask(_,n){try{this._hasTaskZS&&this._hasTaskZS.onHasTask(this._hasTaskDlgt,this._hasTaskCurrZone,_,n)}catch(o){this.handleError(_,o)}}_updateTaskCount(_,n){const o=this._taskCounts,m=o[_],P=o[_]=m+n;if(P<0)throw new Error("More tasks executed then were scheduled.");0!=m&&0!=P||this.hasTask(this._zone,{microTask:o.microTask>0,macroTask:o.macroTask>0,eventTask:o.eventTask>0,change:_})}}class E{constructor(_,n,o,m,P,q){if(this._zone=null,this.runCount=0,this._zoneDelegates=null,this._state="notScheduled",this.type=_,this.source=n,this.data=m,this.scheduleFn=P,this.cancelFn=q,!o)throw new Error("callback is not defined");this.callback=o;const A=this;this.invoke=_===z&&m&&m.useG?E.invokeTask:function(){return E.invokeTask.call(te,A,this,arguments)}}static invokeTask(_,n,o){_||(_=this),ee++;try{return _.runCount++,_.zone.runTask(_,n,o)}finally{1==ee&&K(),ee--}}get zone(){return this._zone}get state(){return this._state}cancelScheduleRequest(){this._transitionTo(X,k)}_transitionTo(_,n,o){if(this._state!==n&&this._state!==o)throw new Error(`${this.type} '${this.source}': can not transition to '${_}', expecting state '${n}'${o?" or '"+o+"'":""}, was '${this._state}'.`);this._state=_,_==X&&(this._zoneDelegates=null)}toString(){return this.data&&typeof this.data.handleId<"u"?this.data.handleId.toString():Object.prototype.toString.call(this)}toJSON(){return{type:this.type,state:this.state,source:this.source,zone:this.zone.name,runCount:this.runCount}}}const T=Q("setTimeout"),y=Q("Promise"),D=Q("then");let Z,d=[],w=!1;function x(N){if(Z||te[y]&&(Z=te[y].resolve(0)),Z){let _=Z[D];_||(_=Z.then),_.call(Z,N)}else te[T](N,0)}function U(N){0===ee&&0===d.length&&x(K),N&&d.push(N)}function K(){if(!w){for(w=!0;d.length;){const N=d;d=[];for(let _=0;_<N.length;_++){const n=N[_];try{n.zone.runTask(n,null,null)}catch(o){R.onUnhandledError(o)}}}R.microtaskDrainDone(),w=!1}}const J={name:"NO ZONE"},X="notScheduled",k="scheduling",h="scheduled",H="running",V="canceling",Y="unknown",G="microTask",g="macroTask",z="eventTask",O={},R={symbol:Q,currentZoneFrame:()=>b,onUnhandledError:W,microtaskDrainDone:W,scheduleMicroTask:U,showUncaughtError:()=>!t[Q("ignoreConsoleErrorUncaughtError")],patchEventTarget:()=>[],patchOnProperties:W,patchMethod:()=>W,bindArguments:()=>[],patchThen:()=>W,patchMacroTask:()=>W,patchEventPrototype:()=>W,isIEOrEdge:()=>!1,getGlobalObjects:()=>{},ObjectDefineProperty:()=>W,ObjectGetOwnPropertyDescriptor:()=>{},ObjectCreate:()=>{},ArraySlice:()=>[],patchClass:()=>W,wrapWithCurrentZone:()=>W,filterProperties:()=>[],attachOriginToPatched:()=>W,_redefineProperty:()=>W,patchCallbacks:()=>W,nativeScheduleMicroTask:x};let b={parent:null,zone:new t(null,null)},S=null,ee=0;function W(){}return c("Zone","Zone"),t}(),e.Zone}();(function Zt(e){(function Nt(e){e.__load_patch("ZoneAwarePromise",(r,c,t)=>{const i=Object.getOwnPropertyDescriptor,u=Object.defineProperty,T=t.symbol,y=[],D=!1!==r[T("DISABLE_WRAPPING_UNCAUGHT_PROMISE_REJECTION")],d=T("Promise"),w=T("then");t.onUnhandledError=f=>{if(t.showUncaughtError()){const a=f&&f.rejection;a?console.error("Unhandled Promise rejection:",a instanceof Error?a.message:a,"; Zone:",f.zone.name,"; Task:",f.task&&f.task.source,"; Value:",a,a instanceof Error?a.stack:void 0):console.error(f)}},t.microtaskDrainDone=()=>{for(;y.length;){const f=y.shift();try{f.zone.runGuarded(()=>{throw f.throwOriginal?f.rejection:f})}catch(a){U(a)}}};const x=T("unhandledPromiseRejectionHandler");function U(f){t.onUnhandledError(f);try{const a=c[x];"function"==typeof a&&a.call(this,f)}catch{}}function K(f){return f&&f.then}function J(f){return f}function X(f){return A.reject(f)}const k=T("state"),h=T("value"),H=T("finally"),V=T("parentPromiseValue"),Y=T("parentPromiseState"),g=null,z=!0,O=!1;function b(f,a){return s=>{try{N(f,a,s)}catch(l){N(f,!1,l)}}}const S=function(){let f=!1;return function(s){return function(){f||(f=!0,s.apply(null,arguments))}}},ee="Promise resolved with itself",W=T("currentTaskTrace");function N(f,a,s){const l=S();if(f===s)throw new TypeError(ee);if(f[k]===g){let v=null;try{("object"==typeof s||"function"==typeof s)&&(v=s&&s.then)}catch(C){return l(()=>{N(f,!1,C)})(),f}if(a!==O&&s instanceof A&&s.hasOwnProperty(k)&&s.hasOwnProperty(h)&&s[k]!==g)n(s),N(f,s[k],s[h]);else if(a!==O&&"function"==typeof v)try{v.call(s,l(b(f,a)),l(b(f,!1)))}catch(C){l(()=>{N(f,!1,C)})()}else{f[k]=a;const C=f[h];if(f[h]=s,f[H]===H&&a===z&&(f[k]=f[Y],f[h]=f[V]),a===O&&s instanceof Error){const p=c.currentTask&&c.currentTask.data&&c.currentTask.data.__creationTrace__;p&&u(s,W,{configurable:!0,enumerable:!1,writable:!0,value:p})}for(let p=0;p<C.length;)o(f,C[p++],C[p++],C[p++],C[p++]);if(0==C.length&&a==O){f[k]=0;let p=s;try{throw new Error("Uncaught (in promise): "+function E(f){return f&&f.toString===Object.prototype.toString?(f.constructor&&f.constructor.name||"")+": "+JSON.stringify(f):f?f.toString():Object.prototype.toString.call(f)}(s)+(s&&s.stack?"\n"+s.stack:""))}catch(L){p=L}D&&(p.throwOriginal=!0),p.rejection=s,p.promise=f,p.zone=c.current,p.task=c.currentTask,y.push(p),t.scheduleMicroTask()}}}return f}const _=T("rejectionHandledHandler");function n(f){if(0===f[k]){try{const a=c[_];a&&"function"==typeof a&&a.call(this,{rejection:f[h],promise:f})}catch{}f[k]=O;for(let a=0;a<y.length;a++)f===y[a].promise&&y.splice(a,1)}}function o(f,a,s,l,v){n(f);const C=f[k],p=C?"function"==typeof l?l:J:"function"==typeof v?v:X;a.scheduleMicroTask("Promise.then",()=>{try{const L=f[h],I=!!s&&H===s[H];I&&(s[V]=L,s[Y]=C);const M=a.run(p,void 0,I&&p!==X&&p!==J?[]:[L]);N(s,!0,M)}catch(L){N(s,!1,L)}},s)}const P=function(){},q=r.AggregateError;class A{static toString(){return"function ZoneAwarePromise() { [native code] }"}static resolve(a){return a instanceof A?a:N(new this(null),z,a)}static reject(a){return N(new this(null),O,a)}static withResolvers(){const a={};return a.promise=new A((s,l)=>{a.resolve=s,a.reject=l}),a}static any(a){if(!a||"function"!=typeof a[Symbol.iterator])return Promise.reject(new q([],"All promises were rejected"));const s=[];let l=0;try{for(let p of a)l++,s.push(A.resolve(p))}catch{return Promise.reject(new q([],"All promises were rejected"))}if(0===l)return Promise.reject(new q([],"All promises were rejected"));let v=!1;const C=[];return new A((p,L)=>{for(let I=0;I<s.length;I++)s[I].then(M=>{v||(v=!0,p(M))},M=>{C.push(M),l--,0===l&&(v=!0,L(new q(C,"All promises were rejected")))})})}static race(a){let s,l,v=new this((L,I)=>{s=L,l=I});function C(L){s(L)}function p(L){l(L)}for(let L of a)K(L)||(L=this.resolve(L)),L.then(C,p);return v}static all(a){return A.allWithCallback(a)}static allSettled(a){return(this&&this.prototype instanceof A?this:A).allWithCallback(a,{thenCallback:l=>({status:"fulfilled",value:l}),errorCallback:l=>({status:"rejected",reason:l})})}static allWithCallback(a,s){let l,v,C=new this((M,B)=>{l=M,v=B}),p=2,L=0;const I=[];for(let M of a){K(M)||(M=this.resolve(M));const B=L;try{M.then(F=>{I[B]=s?s.thenCallback(F):F,p--,0===p&&l(I)},F=>{s?(I[B]=s.errorCallback(F),p--,0===p&&l(I)):v(F)})}catch(F){v(F)}p++,L++}return p-=2,0===p&&l(I),C}constructor(a){const s=this;if(!(s instanceof A))throw new Error("Must be an instanceof Promise.");s[k]=g,s[h]=[];try{const l=S();a&&a(l(b(s,z)),l(b(s,O)))}catch(l){N(s,!1,l)}}get[Symbol.toStringTag](){return"Promise"}get[Symbol.species](){return A}then(a,s){let l=this.constructor?.[Symbol.species];(!l||"function"!=typeof l)&&(l=this.constructor||A);const v=new l(P),C=c.current;return this[k]==g?this[h].push(C,v,a,s):o(this,C,v,a,s),v}catch(a){return this.then(null,a)}finally(a){let s=this.constructor?.[Symbol.species];(!s||"function"!=typeof s)&&(s=A);const l=new s(P);l[H]=H;const v=c.current;return this[k]==g?this[h].push(v,l,a,a):o(this,v,l,a,a),l}}A.resolve=A.resolve,A.reject=A.reject,A.race=A.race,A.all=A.all;const _e=r[d]=r.Promise;r.Promise=A;const he=T("thenPatched");function de(f){const a=f.prototype,s=i(a,"then");if(s&&(!1===s.writable||!s.configurable))return;const l=a.then;a[w]=l,f.prototype.then=function(v,C){return new A((L,I)=>{l.call(this,L,I)}).then(v,C)},f[he]=!0}return t.patchThen=de,_e&&(de(_e),ue(r,"fetch",f=>function oe(f){return function(a,s){let l=f.apply(a,s);if(l instanceof A)return l;let v=l.constructor;return v[he]||de(v),l}}(f))),Promise[c.__symbol__("uncaughtPromiseErrors")]=y,A})})(e),function Lt(e){e.__load_patch("toString",r=>{const c=Function.prototype.toString,t=j("OriginalDelegate"),i=j("Promise"),u=j("Error"),E=function(){if("function"==typeof this){const d=this[t];if(d)return"function"==typeof d?c.call(d):Object.prototype.toString.call(d);if(this===Promise){const w=r[i];if(w)return c.call(w)}if(this===Error){const w=r[u];if(w)return c.call(w)}}return c.call(this)};E[t]=c,Function.prototype.toString=E;const T=Object.prototype.toString;Object.prototype.toString=function(){return"function"==typeof Promise&&this instanceof Promise?"[object Promise]":T.call(this)}})}(e),function Mt(e){e.__load_patch("util",(r,c,t)=>{const i=Fe(r);t.patchOnProperties=Ke,t.patchMethod=ue,t.bindArguments=Ve,t.patchMacroTask=yt;const u=c.__symbol__("BLACK_LISTED_EVENTS"),E=c.__symbol__("UNPATCHED_EVENTS");r[E]&&(r[u]=r[E]),r[u]&&(c[u]=c[E]=r[u]),t.patchEventPrototype=Pt,t.patchEventTarget=bt,t.isIEOrEdge=kt,t.ObjectDefineProperty=Le,t.ObjectGetOwnPropertyDescriptor=Te,t.ObjectCreate=_t,t.ArraySlice=Et,t.patchClass=we,t.wrapWithCurrentZone=He,t.filterProperties=it,t.attachOriginToPatched=fe,t._redefineProperty=Object.defineProperty,t.patchCallbacks=It,t.getGlobalObjects=()=>({globalSources:tt,zoneSymbolEventNames:ne,eventNames:i,isBrowser:Ge,isMix:Xe,isNode:De,TRUE_STR:ae,FALSE_STR:le,ZONE_SYMBOL_PREFIX:Pe,ADD_EVENT_LISTENER_STR:Me,REMOVE_EVENT_LISTENER_STR:Ze})})}(e)})(at),function Ot(e){e.__load_patch("legacy",r=>{const c=r[e.__symbol__("legacyPatch")];c&&c()}),e.__load_patch("timers",r=>{const c="set",t="clear";ye(r,c,t,"Timeout"),ye(r,c,t,"Interval"),ye(r,c,t,"Immediate")}),e.__load_patch("requestAnimationFrame",r=>{ye(r,"request","cancel","AnimationFrame"),ye(r,"mozRequest","mozCancel","AnimationFrame"),ye(r,"webkitRequest","webkitCancel","AnimationFrame")}),e.__load_patch("blocking",(r,c)=>{const t=["alert","prompt","confirm"];for(let i=0;i<t.length;i++)ue(r,t[i],(E,T,y)=>function(D,d){return c.current.run(E,r,d,y)})}),e.__load_patch("EventTarget",(r,c,t)=>{(function Dt(e,r){r.patchEventPrototype(e,r)})(r,t),function Ct(e,r){if(Zone[r.symbol("patchEventTarget")])return;const{eventNames:c,zoneSymbolEventNames:t,TRUE_STR:i,FALSE_STR:u,ZONE_SYMBOL_PREFIX:E}=r.getGlobalObjects();for(let y=0;y<c.length;y++){const D=c[y],Z=E+(D+u),x=E+(D+i);t[D]={},t[D][u]=Z,t[D][i]=x}const T=e.EventTarget;T&&T.prototype&&r.patchEventTarget(e,r,[T&&T.prototype])}(r,t);const i=r.XMLHttpRequestEventTarget;i&&i.prototype&&t.patchEventTarget(r,t,[i.prototype])}),e.__load_patch("MutationObserver",(r,c,t)=>{we("MutationObserver"),we("WebKitMutationObserver")}),e.__load_patch("IntersectionObserver",(r,c,t)=>{we("IntersectionObserver")}),e.__load_patch("FileReader",(r,c,t)=>{we("FileReader")}),e.__load_patch("on_property",(r,c,t)=>{!function St(e,r){if(De&&!Xe||Zone[e.symbol("patchEvents")])return;const c=r.__Zone_ignore_on_properties;let t=[];if(Ge){const i=window;t=t.concat(["Document","SVGElement","Element","HTMLElement","HTMLBodyElement","HTMLMediaElement","HTMLFrameSetElement","HTMLFrameElement","HTMLIFrameElement","HTMLMarqueeElement","Worker"]);const u=function mt(){try{const e=ge.navigator.userAgent;if(-1!==e.indexOf("MSIE ")||-1!==e.indexOf("Trident/"))return!0}catch{}return!1}()?[{target:i,ignoreProperties:["error"]}]:[];ct(i,Fe(i),c&&c.concat(u),Ie(i))}t=t.concat(["XMLHttpRequest","XMLHttpRequestEventTarget","IDBIndex","IDBRequest","IDBOpenDBRequest","IDBDatabase","IDBTransaction","IDBCursor","WebSocket"]);for(let i=0;i<t.length;i++){const u=r[t[i]];u&&u.prototype&&ct(u.prototype,Fe(u.prototype),c)}}(t,r)}),e.__load_patch("customElements",(r,c,t)=>{!function Rt(e,r){const{isBrowser:c,isMix:t}=r.getGlobalObjects();(c||t)&&e.customElements&&"customElements"in e&&r.patchCallbacks(r,e.customElements,"customElements","define",["connectedCallback","disconnectedCallback","adoptedCallback","attributeChangedCallback","formAssociatedCallback","formDisabledCallback","formResetCallback","formStateRestoreCallback"])}(r,t)}),e.__load_patch("XHR",(r,c)=>{!function D(d){const w=d.XMLHttpRequest;if(!w)return;const Z=w.prototype;let U=Z[Ae],K=Z[je];if(!U){const R=d.XMLHttpRequestEventTarget;if(R){const b=R.prototype;U=b[Ae],K=b[je]}}const J="readystatechange",X="scheduled";function k(R){const b=R.data,S=b.target;S[E]=!1,S[y]=!1;const ee=S[u];U||(U=S[Ae],K=S[je]),ee&&K.call(S,J,ee);const W=S[u]=()=>{if(S.readyState===S.DONE)if(!b.aborted&&S[E]&&R.state===X){const _=S[c.__symbol__("loadfalse")];if(0!==S.status&&_&&_.length>0){const n=R.invoke;R.invoke=function(){const o=S[c.__symbol__("loadfalse")];for(let m=0;m<o.length;m++)o[m]===R&&o.splice(m,1);!b.aborted&&R.state===X&&n.call(R)},_.push(R)}else R.invoke()}else!b.aborted&&!1===S[E]&&(S[y]=!0)};return U.call(S,J,W),S[t]||(S[t]=R),z.apply(S,b.args),S[E]=!0,R}function h(){}function H(R){const b=R.data;return b.aborted=!0,O.apply(b.target,b.args)}const V=ue(Z,"open",()=>function(R,b){return R[i]=0==b[2],R[T]=b[1],V.apply(R,b)}),G=j("fetchTaskAborting"),g=j("fetchTaskScheduling"),z=ue(Z,"send",()=>function(R,b){if(!0===c.current[g]||R[i])return z.apply(R,b);{const S={target:R,url:R[T],isPeriodic:!1,args:b,aborted:!1},ee=xe("XMLHttpRequest.send",h,S,k,H);R&&!0===R[y]&&!S.aborted&&ee.state===X&&ee.invoke()}}),O=ue(Z,"abort",()=>function(R,b){const S=function x(R){return R[t]}(R);if(S&&"string"==typeof S.type){if(null==S.cancelFn||S.data&&S.data.aborted)return;S.zone.cancelTask(S)}else if(!0===c.current[G])return O.apply(R,b)})}(r);const t=j("xhrTask"),i=j("xhrSync"),u=j("xhrListener"),E=j("xhrScheduled"),T=j("xhrURL"),y=j("xhrErrorBeforeScheduled")}),e.__load_patch("geolocation",r=>{r.navigator&&r.navigator.geolocation&&function gt(e,r){const c=e.constructor.name;for(let t=0;t<r.length;t++){const i=r[t],u=e[i];if(u){if(!We(Te(e,i)))continue;e[i]=(T=>{const y=function(){return T.apply(this,Ve(arguments,c+"."+i))};return fe(y,T),y})(u)}}}(r.navigator.geolocation,["getCurrentPosition","watchPosition"])}),e.__load_patch("PromiseRejectionEvent",(r,c)=>{function t(i){return function(u){st(r,i).forEach(T=>{const y=r.PromiseRejectionEvent;if(y){const D=new y(i,{promise:u.promise,reason:u.rejection});T.invoke(D)}})}}r.PromiseRejectionEvent&&(c[j("unhandledPromiseRejectionHandler")]=t("unhandledrejection"),c[j("rejectionHandledHandler")]=t("rejectionhandled"))}),e.__load_patch("queueMicrotask",(r,c,t)=>{!function wt(e,r){r.patchMethod(e,"queueMicrotask",c=>function(t,i){Zone.current.scheduleMicroTask("queueMicrotask",i[0])})}(r,t)})}(at)}},te=>{te(te.s=50)}]);

"use strict";(self.webpackChunkcoverage_app=self.webpackChunkcoverage_app||[]).push([[792],{551:()=>{let Wo;function Os(){return Wo}function un(e){const n=Wo;return Wo=e,n}const fc=Symbol("NotFound");class l1 extends Error{name="\u0275NotFound";constructor(n){super(n)}}function Ns(e){return e===fc||"\u0275NotFound"===e?.name}function hc(e,n){return Object.is(e,n)}let Pe=null,Ki=!1,gc=1;const Ye=Symbol("SIGNAL");function $(e){const n=Pe;return Pe=e,n}const Yo={version:0,lastCleanEpoch:0,dirty:!1,producerNode:void 0,producerLastReadVersion:void 0,producerIndexOfThis:void 0,nextProducerIndex:0,liveConsumerNode:void 0,liveConsumerIndexOfThis:void 0,consumerAllowSignalWrites:!1,consumerIsAlwaysLive:!1,kind:"unknown",producerMustRecompute:()=>!1,producerRecomputeValue:()=>{},consumerMarkedDirty:()=>{},consumerOnSignalRead:()=>{}};function As(e){if(Ki)throw new Error("");if(null===Pe)return;Pe.consumerOnSignalRead(e);const n=Pe.nextProducerIndex++;Ls(Pe),n<Pe.producerNode.length&&Pe.producerNode[n]!==e&&tr(Pe)&&Fs(Pe.producerNode[n],Pe.producerIndexOfThis[n]),Pe.producerNode[n]!==e&&(Pe.producerNode[n]=e,Pe.producerIndexOfThis[n]=tr(Pe)?ug(e,Pe,n):0),Pe.producerLastReadVersion[n]=e.version}function Xi(e){if((!tr(e)||e.dirty)&&(e.dirty||e.lastCleanEpoch!==gc)){if(!e.producerMustRecompute(e)&&!ks(e))return void xs(e);e.producerRecomputeValue(e),xs(e)}}function lg(e){if(void 0===e.liveConsumerNode)return;const n=Ki;Ki=!0;try{for(const t of e.liveConsumerNode)t.dirty||u1(t)}finally{Ki=n}}function cg(){return!1!==Pe?.consumerAllowSignalWrites}function u1(e){e.dirty=!0,lg(e),e.consumerMarkedDirty?.(e)}function xs(e){e.dirty=!1,e.lastCleanEpoch=gc}function Jo(e){return e&&(e.nextProducerIndex=0),$(e)}function er(e,n){if($(n),e&&void 0!==e.producerNode&&void 0!==e.producerIndexOfThis&&void 0!==e.producerLastReadVersion){if(tr(e))for(let t=e.nextProducerIndex;t<e.producerNode.length;t++)Fs(e.producerNode[t],e.producerIndexOfThis[t]);for(;e.producerNode.length>e.nextProducerIndex;)e.producerNode.pop(),e.producerLastReadVersion.pop(),e.producerIndexOfThis.pop()}}function ks(e){Ls(e);for(let n=0;n<e.producerNode.length;n++){const t=e.producerNode[n],o=e.producerLastReadVersion[n];if(o!==t.version||(Xi(t),o!==t.version))return!0}return!1}function Rs(e){if(Ls(e),tr(e))for(let n=0;n<e.producerNode.length;n++)Fs(e.producerNode[n],e.producerIndexOfThis[n]);e.producerNode.length=e.producerLastReadVersion.length=e.producerIndexOfThis.length=0,e.liveConsumerNode&&(e.liveConsumerNode.length=e.liveConsumerIndexOfThis.length=0)}function ug(e,n,t){if(dg(e),0===e.liveConsumerNode.length&&fg(e))for(let o=0;o<e.producerNode.length;o++)e.producerIndexOfThis[o]=ug(e.producerNode[o],e,o);return e.liveConsumerIndexOfThis.push(t),e.liveConsumerNode.push(n)-1}function Fs(e,n){if(dg(e),1===e.liveConsumerNode.length&&fg(e))for(let o=0;o<e.producerNode.length;o++)Fs(e.producerNode[o],e.producerIndexOfThis[o]);const t=e.liveConsumerNode.length-1;if(e.liveConsumerNode[n]=e.liveConsumerNode[t],e.liveConsumerIndexOfThis[n]=e.liveConsumerIndexOfThis[t],e.liveConsumerNode.length--,e.liveConsumerIndexOfThis.length--,n<e.liveConsumerNode.length){const o=e.liveConsumerIndexOfThis[n],i=e.liveConsumerNode[n];Ls(i),i.producerIndexOfThis[o]=n}}function tr(e){return e.consumerIsAlwaysLive||(e?.liveConsumerNode?.length??0)>0}function Ls(e){e.producerNode??=[],e.producerIndexOfThis??=[],e.producerLastReadVersion??=[]}function dg(e){e.liveConsumerNode??=[],e.liveConsumerIndexOfThis??=[]}function fg(e){return void 0!==e.producerNode}const oo=Symbol("UNSET"),Zo=Symbol("COMPUTING"),Sn=Symbol("ERRORED"),f1={...Yo,value:oo,dirty:!0,error:null,equal:hc,kind:"computed",producerMustRecompute:e=>e.value===oo||e.value===Zo,producerRecomputeValue(e){if(e.value===Zo)throw new Error("");const n=e.value;e.value=Zo;const t=Jo(e);let o,i=!1;try{o=e.computation(),$(null),i=n!==oo&&n!==Sn&&o!==Sn&&e.equal(n,o)}catch(r){o=Sn,e.error=r}finally{er(e,t)}i?e.value=n:(e.value=o,e.version++)}};let hg=function h1(){throw new Error};function gg(e){hg(e)}function p1(e,n){const t=Object.create(mg);t.value=e,void 0!==n&&(t.equal=n);const o=()=>function m1(e){return As(e),e.value}(t);return o[Ye]=t,[o,s=>vc(t,s),s=>function pg(e,n){cg()||gg(e),vc(e,n(e.value))}(t,s)]}function vc(e,n){cg()||gg(e),e.equal(e.value,n)||(e.value=n,function _1(e){e.version++,function c1(){gc++}(),lg(e)}(e))}const mg={...Yo,equal:hc,value:void 0,kind:"signal"};function Re(e){return"function"==typeof e}function _g(e){const t=e(o=>{Error.call(o),o.stack=(new Error).stack});return t.prototype=Object.create(Error.prototype),t.prototype.constructor=t,t}const yc=_g(e=>function(t){e(this),this.message=t?`${t.length} errors occurred during unsubscription:\n${t.map((o,i)=>`${i+1}) ${o.toString()}`).join("\n  ")}`:"",this.name="UnsubscriptionError",this.errors=t});function Vs(e,n){if(e){const t=e.indexOf(n);0<=t&&e.splice(t,1)}}class Et{constructor(n){this.initialTeardown=n,this.closed=!1,this._parentage=null,this._finalizers=null}unsubscribe(){let n;if(!this.closed){this.closed=!0;const{_parentage:t}=this;if(t)if(this._parentage=null,Array.isArray(t))for(const r of t)r.remove(this);else t.remove(this);const{initialTeardown:o}=this;if(Re(o))try{o()}catch(r){n=r instanceof yc?r.errors:[r]}const{_finalizers:i}=this;if(i){this._finalizers=null;for(const r of i)try{Cg(r)}catch(s){n=n??[],s instanceof yc?n=[...n,...s.errors]:n.push(s)}}if(n)throw new yc(n)}}add(n){var t;if(n&&n!==this)if(this.closed)Cg(n);else{if(n instanceof Et){if(n.closed||n._hasParent(this))return;n._addParent(this)}(this._finalizers=null!==(t=this._finalizers)&&void 0!==t?t:[]).push(n)}}_hasParent(n){const{_parentage:t}=this;return t===n||Array.isArray(t)&&t.includes(n)}_addParent(n){const{_parentage:t}=this;this._parentage=Array.isArray(t)?(t.push(n),t):t?[t,n]:n}_removeParent(n){const{_parentage:t}=this;t===n?this._parentage=null:Array.isArray(t)&&Vs(t,n)}remove(n){const{_finalizers:t}=this;t&&Vs(t,n),n instanceof Et&&n._removeParent(this)}}Et.EMPTY=(()=>{const e=new Et;return e.closed=!0,e})();const vg=Et.EMPTY;function yg(e){return e instanceof Et||e&&"closed"in e&&Re(e.remove)&&Re(e.add)&&Re(e.unsubscribe)}function Cg(e){Re(e)?e():e.unsubscribe()}const io={onUnhandledError:null,onStoppedNotification:null,Promise:void 0,useDeprecatedSynchronousErrorHandling:!1,useDeprecatedNextContext:!1},Hs={setTimeout(e,n,...t){const{delegate:o}=Hs;return o?.setTimeout?o.setTimeout(e,n,...t):setTimeout(e,n,...t)},clearTimeout(e){const{delegate:n}=Hs;return(n?.clearTimeout||clearTimeout)(e)},delegate:void 0};function bg(e){Hs.setTimeout(()=>{const{onUnhandledError:n}=io;if(!n)throw e;n(e)})}function Dg(){}const v1=Cc("C",void 0,void 0);function Cc(e,n,t){return{kind:e,value:n,error:t}}let ro=null;function Bs(e){if(io.useDeprecatedSynchronousErrorHandling){const n=!ro;if(n&&(ro={errorThrown:!1,error:null}),e(),n){const{errorThrown:t,error:o}=ro;if(ro=null,t)throw o}}else e()}class bc extends Et{constructor(n){super(),this.isStopped=!1,n?(this.destination=n,yg(n)&&n.add(this)):this.destination=I1}static create(n,t,o){return new wc(n,t,o)}next(n){this.isStopped?Ec(function C1(e){return Cc("N",e,void 0)}(n),this):this._next(n)}error(n){this.isStopped?Ec(function y1(e){return Cc("E",void 0,e)}(n),this):(this.isStopped=!0,this._error(n))}complete(){this.isStopped?Ec(v1,this):(this.isStopped=!0,this._complete())}unsubscribe(){this.closed||(this.isStopped=!0,super.unsubscribe(),this.destination=null)}_next(n){this.destination.next(n)}_error(n){try{this.destination.error(n)}finally{this.unsubscribe()}}_complete(){try{this.destination.complete()}finally{this.unsubscribe()}}}const D1=Function.prototype.bind;function Dc(e,n){return D1.call(e,n)}class w1{constructor(n){this.partialObserver=n}next(n){const{partialObserver:t}=this;if(t.next)try{t.next(n)}catch(o){js(o)}}error(n){const{partialObserver:t}=this;if(t.error)try{t.error(n)}catch(o){js(o)}else js(n)}complete(){const{partialObserver:n}=this;if(n.complete)try{n.complete()}catch(t){js(t)}}}class wc extends bc{constructor(n,t,o){let i;if(super(),Re(n)||!n)i={next:n??void 0,error:t??void 0,complete:o??void 0};else{let r;this&&io.useDeprecatedNextContext?(r=Object.create(n),r.unsubscribe=()=>this.unsubscribe(),i={next:n.next&&Dc(n.next,r),error:n.error&&Dc(n.error,r),complete:n.complete&&Dc(n.complete,r)}):i=n}this.destination=new w1(i)}}function js(e){io.useDeprecatedSynchronousErrorHandling?function b1(e){io.useDeprecatedSynchronousErrorHandling&&ro&&(ro.errorThrown=!0,ro.error=e)}(e):bg(e)}function Ec(e,n){const{onStoppedNotification:t}=io;t&&Hs.setTimeout(()=>t(e,n))}const I1={closed:!0,next:Dg,error:function E1(e){throw e},complete:Dg},Ic="function"==typeof Symbol&&Symbol.observable||"@@observable";function Mc(e){return e}let ft=(()=>{class e{constructor(t){t&&(this._subscribe=t)}lift(t){const o=new e;return o.source=this,o.operator=t,o}subscribe(t,o,i){const r=function T1(e){return e&&e instanceof bc||function M1(e){return e&&Re(e.next)&&Re(e.error)&&Re(e.complete)}(e)&&yg(e)}(t)?t:new wc(t,o,i);return Bs(()=>{const{operator:s,source:a}=this;r.add(s?s.call(r,a):a?this._subscribe(r):this._trySubscribe(r))}),r}_trySubscribe(t){try{return this._subscribe(t)}catch(o){t.error(o)}}forEach(t,o){return new(o=Eg(o))((i,r)=>{const s=new wc({next:a=>{try{t(a)}catch(l){r(l),s.unsubscribe()}},error:r,complete:i});this.subscribe(s)})}_subscribe(t){var o;return null===(o=this.source)||void 0===o?void 0:o.subscribe(t)}[Ic](){return this}pipe(...t){return function wg(e){return 0===e.length?Mc:1===e.length?e[0]:function(t){return e.reduce((o,i)=>i(o),t)}}(t)(this)}toPromise(t){return new(t=Eg(t))((o,i)=>{let r;this.subscribe(s=>r=s,s=>i(s),()=>o(r))})}}return e.create=n=>new e(n),e})();function Eg(e){var n;return null!==(n=e??io.Promise)&&void 0!==n?n:Promise}const S1=_g(e=>function(){e(this),this.name="ObjectUnsubscribedError",this.message="object unsubscribed"});let Kt=(()=>{class e extends ft{constructor(){super(),this.closed=!1,this.currentObservers=null,this.observers=[],this.isStopped=!1,this.hasError=!1,this.thrownError=null}lift(t){const o=new Ig(this,this);return o.operator=t,o}_throwIfClosed(){if(this.closed)throw new S1}next(t){Bs(()=>{if(this._throwIfClosed(),!this.isStopped){this.currentObservers||(this.currentObservers=Array.from(this.observers));for(const o of this.currentObservers)o.next(t)}})}error(t){Bs(()=>{if(this._throwIfClosed(),!this.isStopped){this.hasError=this.isStopped=!0,this.thrownError=t;const{observers:o}=this;for(;o.length;)o.shift().error(t)}})}complete(){Bs(()=>{if(this._throwIfClosed(),!this.isStopped){this.isStopped=!0;const{observers:t}=this;for(;t.length;)t.shift().complete()}})}unsubscribe(){this.isStopped=this.closed=!0,this.observers=this.currentObservers=null}get observed(){var t;return(null===(t=this.observers)||void 0===t?void 0:t.length)>0}_trySubscribe(t){return this._throwIfClosed(),super._trySubscribe(t)}_subscribe(t){return this._throwIfClosed(),this._checkFinalizedStatuses(t),this._innerSubscribe(t)}_innerSubscribe(t){const{hasError:o,isStopped:i,observers:r}=this;return o||i?vg:(this.currentObservers=null,r.push(t),new Et(()=>{this.currentObservers=null,Vs(r,t)}))}_checkFinalizedStatuses(t){const{hasError:o,thrownError:i,isStopped:r}=this;o?t.error(i):r&&t.complete()}asObservable(){const t=new ft;return t.source=this,t}}return e.create=(n,t)=>new Ig(n,t),e})();class Ig extends Kt{constructor(n,t){super(),this.destination=n,this.source=t}next(n){var t,o;null===(o=null===(t=this.destination)||void 0===t?void 0:t.next)||void 0===o||o.call(t,n)}error(n){var t,o;null===(o=null===(t=this.destination)||void 0===t?void 0:t.error)||void 0===o||o.call(t,n)}complete(){var n,t;null===(t=null===(n=this.destination)||void 0===n?void 0:n.complete)||void 0===t||t.call(n)}_subscribe(n){var t,o;return null!==(o=null===(t=this.source)||void 0===t?void 0:t.subscribe(n))&&void 0!==o?o:vg}}class O1 extends Kt{constructor(n){super(),this._value=n}get value(){return this.getValue()}_subscribe(n){const t=super._subscribe(n);return!t.closed&&n.next(this._value),t}getValue(){const{hasError:n,thrownError:t,_value:o}=this;if(n)throw t;return this._throwIfClosed(),o}next(n){super.next(this._value=n)}}const Mg="https://angular.dev/best-practices/security#preventing-cross-site-scripting-xss";class O extends Error{code;constructor(n,t){super(function On(e,n){return`${function N1(e){return`NG0${Math.abs(e)}`}(e)}${n?": "+n:""}`}(n,t)),this.code=n}}const Ee=globalThis;function ye(e){for(let n in e)if(e[n]===ye)return n;throw Error("")}function A1(e,n){for(const t in n)n.hasOwnProperty(t)&&!e.hasOwnProperty(t)&&(e[t]=n[t])}function Ge(e){if("string"==typeof e)return e;if(Array.isArray(e))return`[${e.map(Ge).join(", ")}]`;if(null==e)return""+e;const n=e.overriddenName||e.name;if(n)return`${n}`;const t=e.toString();if(null==t)return""+t;const o=t.indexOf("\n");return o>=0?t.slice(0,o):t}function Tc(e,n){return e?n?`${e} ${n}`:e:n||""}const x1=ye({__forward_ref__:ye});function fe(e){return e.__forward_ref__=fe,e.toString=function(){return Ge(this())},e}function Y(e){return Us(e)?e():e}function Us(e){return"function"==typeof e&&e.hasOwnProperty(x1)&&e.__forward_ref__===fe}function X(e){return{token:e.token,providedIn:e.providedIn||null,factory:e.factory,value:void 0}}function dn(e){return{providers:e.providers||[],imports:e.imports||[]}}function $s(e){return function L1(e,n){return e.hasOwnProperty(n)&&e[n]||null}(e,Gs)}function zs(e){return e&&e.hasOwnProperty(Sc)?e[Sc]:null}const Gs=ye({\u0275prov:ye}),Sc=ye({\u0275inj:ye});class R{_desc;ngMetadataName="InjectionToken";\u0275prov;constructor(n,t){this._desc=n,this.\u0275prov=void 0,"number"==typeof t?this.__NG_ELEMENT_ID__=t:void 0!==t&&(this.\u0275prov=X({token:this,providedIn:t.providedIn||"root",factory:t.factory}))}get multi(){return this}toString(){return`InjectionToken ${this._desc}`}}function Nc(e){return e&&!!e.\u0275providers}const Ac=ye({\u0275cmp:ye}),j1=ye({\u0275dir:ye}),U1=ye({\u0275pipe:ye}),Sg=ye({\u0275mod:ye}),ao=ye({\u0275fac:ye}),ir=ye({__NG_ELEMENT_ID__:ye}),Og=ye({__NG_ENV_ID__:ye});function W(e){return"string"==typeof e?e:null==e?"":String(e)}function Ng(e,n){throw new O(-200,e)}function xc(e,n){throw new O(-201,!1)}let kc;function Ag(){return kc}function ht(e){const n=kc;return kc=e,n}function xg(e,n,t){const o=$s(e);return o&&"root"==o.providedIn?void 0===o.value?o.value=o.factory():o.value:8&t?null:void 0!==n?n:void xc()}const lo={},Rc="__NG_DI_FLAG__";class G1{injector;constructor(n){this.injector=n}retrieve(n,t){const o=rr(t)||0;try{return this.injector.get(n,8&o?null:lo,o)}catch(i){if(Ns(i))return i;throw i}}}const qs="ngTempTokenPath",W1=/\n/gm,kg="__source";function J1(e,n=0){const t=Os();if(void 0===t)throw new O(-203,!1);if(null===t)return xg(e,void 0,n);{const o=function Z1(e){return{optional:!!(8&e),host:!!(1&e),self:!!(2&e),skipSelf:!!(4&e)}}(n),i=t.retrieve(e,o);if(Ns(i)){if(o.optional)return null;throw i}return i}}function te(e,n=0){return(Ag()||J1)(Y(e),n)}function V(e,n){return te(e,rr(n))}function rr(e){return typeof e>"u"||"number"==typeof e?e:0|(e.optional&&8)|(e.host&&1)|(e.self&&2)|(e.skipSelf&&4)}function Fc(e){const n=[];for(let t=0;t<e.length;t++){const o=Y(e[t]);if(Array.isArray(o)){if(0===o.length)throw new O(900,!1);let i,r=0;for(let s=0;s<o.length;s++){const a=o[s],l=Q1(a);"number"==typeof l?-1===l?i=a.token:r|=l:i=a}n.push(te(i,r))}else n.push(te(o))}return n}function sr(e,n){return e[Rc]=n,e.prototype[Rc]=n,e}function Q1(e){return e[Rc]}function co(e,n){return e.hasOwnProperty(ao)?e[ao]:null}function Qo(e,n){e.forEach(t=>Array.isArray(t)?Qo(t,n):n(t))}function Fg(e,n,t){n>=e.length?e.push(t):e.splice(n,0,t)}function Ws(e,n){return n>=e.length-1?e.pop():e.splice(n,1)[0]}function Js(e,n,t){let o=ar(e,n);return o>=0?e[1|o]=t:(o=~o,function Pg(e,n,t,o){let i=e.length;if(i==n)e.push(t,o);else if(1===i)e.push(o,e[0]),e[0]=t;else{for(i--,e.push(e[i-1],e[i]);i>n;)e[i]=e[i-2],i--;e[n]=t,e[n+1]=o}}(e,o,n,t)),o}function Lc(e,n){const t=ar(e,n);if(t>=0)return e[1|t]}function ar(e,n){return function tI(e,n,t){let o=0,i=e.length>>t;for(;i!==o;){const r=o+(i-o>>1),s=e[r<<t];if(n===s)return r<<t;s>n?i=r:o=r+1}return~(i<<t)}(e,n,1)}const Xt={},pe=[],uo=new R(""),Vg=new R("",-1),Pc=new R("");class Zs{get(n,t=lo){if(t===lo)throw new l1(`NullInjectorError: No provider for ${Ge(n)}!`);return t}}function ie(e){return e[Ac]||null}function ot(e){return e[j1]||null}function tn(e){return e[U1]||null}function oI(...e){return{\u0275providers:Hc(0,e),\u0275fromNgModule:!0}}function Hc(e,...n){const t=[],o=new Set;let i;const r=s=>{t.push(s)};return Qo(n,s=>{const a=s;Qs(a,r,[],o)&&(i||=[],i.push(a))}),void 0!==i&&Bg(i,r),t}function Bg(e,n){for(let t=0;t<e.length;t++){const{ngModule:o,providers:i}=e[t];Bc(i,r=>{n(r,o)})}}function Qs(e,n,t,o){if(!(e=Y(e)))return!1;let i=null,r=zs(e);const s=!r&&ie(e);if(r||s){if(s&&!s.standalone)return!1;i=e}else{const l=e.ngModule;if(r=zs(l),!r)return!1;i=l}const a=o.has(i);if(s){if(a)return!1;if(o.add(i),s.dependencies){const l="function"==typeof s.dependencies?s.dependencies():s.dependencies;for(const c of l)Qs(c,n,t,o)}}else{if(!r)return!1;{if(null!=r.imports&&!a){let c;o.add(i);try{Qo(r.imports,u=>{Qs(u,n,t,o)&&(c||=[],c.push(u))})}finally{}void 0!==c&&Bg(c,n)}if(!a){const c=co(i)||(()=>new i);n({provide:i,useFactory:c,deps:pe},i),n({provide:Pc,useValue:i,multi:!0},i),n({provide:uo,useValue:()=>te(i),multi:!0},i)}const l=r.providers;if(null!=l&&!a){const c=e;Bc(l,u=>{n(u,c)})}}}return i!==e&&void 0!==e.providers}function Bc(e,n){for(let t of e)Nc(t)&&(t=t.\u0275providers),Array.isArray(t)?Bc(t,n):n(t)}const iI=ye({provide:String,useValue:ye});function jc(e){return null!==e&&"object"==typeof e&&iI in e}function An(e){return"function"==typeof e}const Uc=new R(""),Ks={},$g={};let $c;function zc(){return void 0===$c&&($c=new Zs),$c}class Rt{}class fo extends Rt{parent;source;scopes;records=new Map;_ngOnDestroyHooks=new Set;_onDestroyHooks=[];get destroyed(){return this._destroyed}_destroyed=!1;injectorDefTypes;constructor(n,t,o,i){super(),this.parent=t,this.source=o,this.scopes=i,qc(n,s=>this.processProvider(s)),this.records.set(Vg,Ko(void 0,this)),i.has("environment")&&this.records.set(Rt,Ko(void 0,this));const r=this.records.get(Uc);null!=r&&"string"==typeof r.value&&this.scopes.add(r.value),this.injectorDefTypes=new Set(this.get(Pc,pe,{self:!0}))}retrieve(n,t){const o=rr(t)||0;try{return this.get(n,lo,o)}catch(i){if(Ns(i))return i;throw i}}destroy(){cr(this),this._destroyed=!0;const n=$(null);try{for(const o of this._ngOnDestroyHooks)o.ngOnDestroy();const t=this._onDestroyHooks;this._onDestroyHooks=[];for(const o of t)o()}finally{this.records.clear(),this._ngOnDestroyHooks.clear(),this.injectorDefTypes.clear(),$(n)}}onDestroy(n){return cr(this),this._onDestroyHooks.push(n),()=>this.removeOnDestroy(n)}runInContext(n){cr(this);const t=un(this),o=ht(void 0);try{return n()}finally{un(t),ht(o)}}get(n,t=lo,o){if(cr(this),n.hasOwnProperty(Og))return n[Og](this);const i=rr(o),s=un(this),a=ht(void 0);try{if(!(4&i)){let c=this.records.get(n);if(void 0===c){const u=function uI(e){return"function"==typeof e||"object"==typeof e&&"InjectionToken"===e.ngMetadataName}(n)&&$s(n);c=u&&this.injectableDefInScope(u)?Ko(Gc(n),Ks):null,this.records.set(n,c)}if(null!=c)return this.hydrate(n,c)}return(2&i?zc():this.parent).get(n,t=8&i&&t===lo?null:t)}catch(l){if(Ns(l)){if((l[qs]=l[qs]||[]).unshift(Ge(n)),s)throw l;return function K1(e,n,t,o){const i=e[qs];throw n[kg]&&i.unshift(n[kg]),e.message=function X1(e,n,t,o=null){e=e&&"\n"===e.charAt(0)&&"\u0275"==e.charAt(1)?e.slice(2):e;let i=Ge(n);if(Array.isArray(n))i=n.map(Ge).join(" -> ");else if("object"==typeof n){let r=[];for(let s in n)if(n.hasOwnProperty(s)){let a=n[s];r.push(s+":"+("string"==typeof a?JSON.stringify(a):Ge(a)))}i=`{${r.join(", ")}}`}return`${t}${o?"("+o+")":""}[${i}]: ${e.replace(W1,"\n  ")}`}("\n"+e.message,i,t,o),e.ngTokenPath=i,e[qs]=null,e}(l,n,"R3InjectorError",this.source)}throw l}finally{ht(a),un(s)}}resolveInjectorInitializers(){const n=$(null),t=un(this),o=ht(void 0);try{const r=this.get(uo,pe,{self:!0});for(const s of r)s()}finally{un(t),ht(o),$(n)}}toString(){const n=[],t=this.records;for(const o of t.keys())n.push(Ge(o));return`R3Injector[${n.join(", ")}]`}processProvider(n){let t=An(n=Y(n))?n:Y(n&&n.provide);const o=function aI(e){return jc(e)?Ko(void 0,e.useValue):Ko(zg(e),Ks)}(n);if(!An(n)&&!0===n.multi){let i=this.records.get(t);i||(i=Ko(void 0,Ks,!0),i.factory=()=>Fc(i.multi),this.records.set(t,i)),t=n,i.multi.push(n)}this.records.set(t,o)}hydrate(n,t){const o=$(null);try{return t.value===$g?Ng(Ge(n)):t.value===Ks&&(t.value=$g,t.value=t.factory()),"object"==typeof t.value&&t.value&&function cI(e){return null!==e&&"object"==typeof e&&"function"==typeof e.ngOnDestroy}(t.value)&&this._ngOnDestroyHooks.add(t.value),t.value}finally{$(o)}}injectableDefInScope(n){if(!n.providedIn)return!1;const t=Y(n.providedIn);return"string"==typeof t?"any"===t||this.scopes.has(t):this.injectorDefTypes.has(t)}removeOnDestroy(n){const t=this._onDestroyHooks.indexOf(n);-1!==t&&this._onDestroyHooks.splice(t,1)}}function Gc(e){const n=$s(e),t=null!==n?n.factory:co(e);if(null!==t)return t;if(e instanceof R)throw new O(204,!1);if(e instanceof Function)return function sI(e){if(e.length>0)throw new O(204,!1);const t=function P1(e){return(e?.[Gs]??null)||null}(e);return null!==t?()=>t.factory(e):()=>new e}(e);throw new O(204,!1)}function zg(e,n,t){let o;if(An(e)){const i=Y(e);return co(i)||Gc(i)}if(jc(e))o=()=>Y(e.useValue);else if(function Ug(e){return!(!e||!e.useFactory)}(e))o=()=>e.useFactory(...Fc(e.deps||[]));else if(function jg(e){return!(!e||!e.useExisting)}(e))o=()=>te(Y(e.useExisting));else{const i=Y(e&&(e.useClass||e.provide));if(!function lI(e){return!!e.deps}(e))return co(i)||Gc(i);o=()=>new i(...Fc(e.deps))}return o}function cr(e){if(e.destroyed)throw new O(205,!1)}function Ko(e,n,t=!1){return{factory:e,value:n,multi:t?[]:void 0}}function qc(e,n){for(const t of e)Array.isArray(t)?qc(t,n):t&&Nc(t)?qc(t.\u0275providers,n):n(t)}function Gg(e,n){let t;e instanceof fo?(cr(e),t=e):t=new G1(e);const i=un(t),r=ht(void 0);try{return n()}finally{un(i),ht(r)}}function Wc(){return void 0!==Ag()||null!=Os()}const q=26;function Te(e){return Array.isArray(e)&&"object"==typeof e[1]}function it(e){return Array.isArray(e)&&!0===e[1]}function Zc(e){return!!(4&e.flags)}function Rn(e){return e.componentOffset>-1}function ta(e){return!(1&~e.flags)}function Tt(e){return!!e.template}function Fn(e){return!!(512&e[2])}function gn(e){return!(256&~e[2])}function $e(e){for(;Array.isArray(e);)e=e[0];return e}function oi(e,n){return $e(n[e])}function gt(e,n){return $e(n[e.index])}function ii(e,n){return e.data[n]}function rt(e,n){const t=n[e];return Te(t)?t:t[0]}function Kc(e){return!(128&~e[2])}function pt(e,n){return null==n?null:e[n]}function Kg(e){e[17]=0}function Xg(e){1024&e[2]||(e[2]|=1024,Kc(e)&&ri(e))}function oa(e){return!!(9216&e[2]||e[24]?.dirty)}function Xc(e){e[10].changeDetectionScheduler?.notify(8),64&e[2]&&(e[2]|=1024),oa(e)&&ri(e)}function ri(e){e[10].changeDetectionScheduler?.notify(0);let n=pn(e);for(;null!==n&&!(8192&n[2])&&(n[2]|=8192,Kc(n));)n=pn(n)}function ia(e,n){if(gn(e))throw new O(911,!1);null===e[21]&&(e[21]=[]),e[21].push(n)}function pn(e){const n=e[3];return it(n)?n[3]:n}function tp(e){return e[7]??=[]}function np(e){return e.cleanup??=[]}const z={lFrame:fp(null),bindingsEnabled:!0,skipHydrationRootTNode:null};let nu=!1;function ou(){return z.bindingsEnabled}function w(){return z.lFrame.lView}function J(){return z.lFrame.tView}function H(e){return z.lFrame.contextLView=e,e[8]}function B(e){return z.lFrame.contextLView=null,e}function K(){let e=ip();for(;null!==e&&64===e.type;)e=e.parent;return e}function ip(){return z.lFrame.currentTNode}function zt(e,n){const t=z.lFrame;t.currentTNode=e,t.isParent=n}function iu(){return z.lFrame.isParent}function ru(){z.lFrame.isParent=!1}function ap(){return nu}function ra(e){const n=nu;return nu=e,n}function st(){const e=z.lFrame;let n=e.bindingRootIndex;return-1===n&&(n=e.bindingRootIndex=e.tView.bindingStartIndex),n}function St(){return z.lFrame.bindingIndex++}function _n(e){const n=z.lFrame,t=n.bindingIndex;return n.bindingIndex=n.bindingIndex+e,t}function TI(e,n){const t=z.lFrame;t.bindingIndex=t.bindingRootIndex=e,su(n)}function su(e){z.lFrame.currentDirectiveIndex=e}function lu(){return z.lFrame.currentQueryIndex}function sa(e){z.lFrame.currentQueryIndex=e}function OI(e){const n=e[1];return 2===n.type?n.declTNode:1===n.type?e[5]:null}function up(e,n,t){if(4&t){let i=n,r=e;for(;!(i=i.parent,null!==i||1&t||(i=OI(r),null===i||(r=r[14],10&i.type))););if(null===i)return!1;n=i,e=r}const o=z.lFrame=dp();return o.currentTNode=n,o.lView=e,!0}function cu(e){const n=dp(),t=e[1];z.lFrame=n,n.currentTNode=t.firstChild,n.lView=e,n.tView=t,n.contextLView=e,n.bindingIndex=t.bindingStartIndex,n.inI18n=!1}function dp(){const e=z.lFrame,n=null===e?null:e.child;return null===n?fp(e):n}function fp(e){const n={currentTNode:null,isParent:!0,lView:null,tView:null,selectedIndex:-1,contextLView:null,elementDepthCount:0,currentNamespace:null,currentDirectiveIndex:-1,bindingRootIndex:-1,bindingIndex:-1,currentQueryIndex:0,parent:e,child:null,inI18n:!1};return null!==e&&(e.child=n),n}function hp(){const e=z.lFrame;return z.lFrame=e.parent,e.currentTNode=null,e.lView=null,e}const gp=hp;function uu(){const e=hp();e.isParent=!0,e.tView=null,e.selectedIndex=-1,e.contextLView=null,e.elementDepthCount=0,e.currentDirectiveIndex=-1,e.currentNamespace=null,e.bindingRootIndex=-1,e.bindingIndex=-1,e.currentQueryIndex=0}function qe(){return z.lFrame.selectedIndex}function vo(e){z.lFrame.selectedIndex=e}function nn(){const e=z.lFrame;return ii(e.tView,e.selectedIndex)}let mp=!0;function hr(){return mp}function gr(e){mp=e}function _p(e,n=null,t=null,o){const i=vp(e,n,t,o);return i.resolveInjectorInitializers(),i}function vp(e,n=null,t=null,o,i=new Set){const r=[t||pe,oI(e)];return o=o||("object"==typeof e?void 0:Ge(e)),new fo(r,n||zc(),o||null,i)}class Lt{static THROW_IF_NOT_FOUND=lo;static NULL=new Zs;static create(n,t){if(Array.isArray(n))return _p({name:""},t,n,"");{const o=n.name??"";return _p({name:o},n.parent,n.providers,o)}}static \u0275prov=X({token:Lt,providedIn:"any",factory:()=>te(Vg)});static __NG_ELEMENT_ID__=-1}const Ln=new R("");let yo=(()=>class e{static __NG_ELEMENT_ID__=FI;static __NG_ENV_ID__=t=>t})();class yp extends yo{_lView;constructor(n){super(),this._lView=n}get destroyed(){return gn(this._lView)}onDestroy(n){const t=this._lView;return ia(t,n),()=>function eu(e,n){if(null===e[21])return;const t=e[21].indexOf(n);-1!==t&&e[21].splice(t,1)}(t,n)}}function FI(){return new yp(w())}class ai{_console=console;handleError(n){this._console.error("ERROR",n)}}const vn=new R("",{providedIn:"root",factory:()=>{const e=V(Rt);let n;return t=>{n??=e.get(ai),n.handleError(t)}}}),LI={provide:uo,useValue:()=>{V(ai)},multi:!0};function Co(e,n){const[t,o,i]=p1(e,n?.equal),r=t;return r.set=o,r.update=i,r.asReadonly=du.bind(r),r}function du(){const e=this[Ye];if(void 0===e.readonlyFn){const n=()=>this();n[Ye]=e,e.readonlyFn=n}return e.readonlyFn}function bp(e){return function Cp(e){return"function"==typeof e&&void 0!==e[Ye]}(e)&&"function"==typeof e.set}class li{}const Dp=new R("",{providedIn:"root",factory:()=>!1}),wp=new R(""),Ep=new R("");let ci=(()=>{class e{taskId=0;pendingTasks=new Set;destroyed=!1;pendingTask=new O1(!1);get hasPendingTasks(){return!this.destroyed&&this.pendingTask.value}get hasPendingTasksObservable(){return this.destroyed?new ft(t=>{t.next(!1),t.complete()}):this.pendingTask}add(){!this.hasPendingTasks&&!this.destroyed&&this.pendingTask.next(!0);const t=this.taskId++;return this.pendingTasks.add(t),t}has(t){return this.pendingTasks.has(t)}remove(t){this.pendingTasks.delete(t),0===this.pendingTasks.size&&this.hasPendingTasks&&this.pendingTask.next(!1)}ngOnDestroy(){this.pendingTasks.clear(),this.hasPendingTasks&&this.pendingTask.next(!1),this.destroyed=!0,this.pendingTask.unsubscribe()}static \u0275prov=X({token:e,providedIn:"root",factory:()=>new e})}return e})();function pr(...e){}let Mp=(()=>{class e{static \u0275prov=X({token:e,providedIn:"root",factory:()=>new HI})}return e})();class HI{dirtyEffectCount=0;queues=new Map;add(n){this.enqueue(n),this.schedule(n)}schedule(n){n.dirty&&this.dirtyEffectCount++}remove(n){const o=this.queues.get(n.zone);o.has(n)&&(o.delete(n),n.dirty&&this.dirtyEffectCount--)}enqueue(n){const t=n.zone;this.queues.has(t)||this.queues.set(t,new Set);const o=this.queues.get(t);o.has(n)||o.add(n)}flush(){for(;this.dirtyEffectCount>0;){let n=!1;for(const[t,o]of this.queues)n||=null===t?this.flushQueue(o):t.run(()=>this.flushQueue(o));n||(this.dirtyEffectCount=0)}}flushQueue(n){let t=!1;for(const o of n)o.dirty&&(this.dirtyEffectCount--,t=!0,o.run());return t}}let Tp=null;function mr(){return Tp}class jI{}function bo(e){return n=>{if(function qI(e){return Re(e?.lift)}(n))return n.lift(function(t){try{return e(t,this)}catch(o){this.error(o)}});throw new TypeError("Unable to lift unknown Observable type")}}function Pn(e,n,t,o,i){return new WI(e,n,t,o,i)}class WI extends bc{constructor(n,t,o,i,r,s){super(n),this.onFinalize=r,this.shouldUnsubscribe=s,this._next=t?function(a){try{t(a)}catch(l){n.error(l)}}:super._next,this._error=i?function(a){try{i(a)}catch(l){n.error(l)}finally{this.unsubscribe()}}:super._error,this._complete=o?function(){try{o()}catch(a){n.error(a)}finally{this.unsubscribe()}}:super._complete}unsubscribe(){var n;if(!this.shouldUnsubscribe||this.shouldUnsubscribe()){const{closed:t}=this;super.unsubscribe(),!t&&(null===(n=this.onFinalize)||void 0===n||n.call(this))}}}function hu(e,n){return bo((t,o)=>{let i=0;t.subscribe(Pn(o,r=>{o.next(e.call(n,r,i++))}))})}function yn(e){return{toString:e}.toString()}const di="__parameters__";function hi(e,n,t){return yn(()=>{const o=function gu(e){return function(...t){if(e){const o=e(...t);for(const i in o)this[i]=o[i]}}}(n);function i(...r){if(this instanceof i)return o.apply(this,r),this;const s=new i(...r);return a.annotation=s,a;function a(l,c,u){const d=l.hasOwnProperty(di)?l[di]:Object.defineProperty(l,di,{value:[]})[di];for(;d.length<=u;)d.push(null);return(d[u]=d[u]||[]).push(s),l}}return i.prototype.ngMetadataName=e,i.annotationCls=i,i})}const pu=sr(hi("Optional"),8),mu=sr(hi("SkipSelf"),4);class iM{previousValue;currentValue;firstChange;constructor(n,t,o){this.previousValue=n,this.currentValue=t,this.firstChange=o}isFirstChange(){return this.firstChange}}function Ap(e,n,t,o){null!==n?n.applyValueToInputSignal(n,o):e[t]=o}const Cn=(()=>{const e=()=>xp;return e.ngInherit=!0,e})();function xp(e){return e.type.prototype.ngOnChanges&&(e.setInput=sM),rM}function rM(){const e=Rp(this),n=e?.current;if(n){const t=e.previous;if(t===Xt)e.previous=n;else for(let o in n)t[o]=n[o];e.current=null,this.ngOnChanges(n)}}function sM(e,n,t,o,i){const r=this.declaredInputs[o],s=Rp(e)||function aM(e,n){return e[kp]=n}(e,{previous:Xt,current:null}),a=s.current||(s.current={}),l=s.previous,c=l[r];a[r]=new iM(c&&c.currentValue,t,l===Xt),Ap(e,n,i,t)}const kp="__ngSimpleChanges__";function Rp(e){return e[kp]||null}const Do=[],ce=function(e,n=null,t){for(let o=0;o<Do.length;o++)(0,Do[o])(e,n,t)};function vu(e,n){for(let t=n.directiveStart,o=n.directiveEnd;t<o;t++){const r=e.data[t].type.prototype,{ngAfterContentInit:s,ngAfterContentChecked:a,ngAfterViewInit:l,ngAfterViewChecked:c,ngOnDestroy:u}=r;s&&(e.contentHooks??=[]).push(-t,s),a&&((e.contentHooks??=[]).push(t,a),(e.contentCheckHooks??=[]).push(t,a)),l&&(e.viewHooks??=[]).push(-t,l),c&&((e.viewHooks??=[]).push(t,c),(e.viewCheckHooks??=[]).push(t,c)),null!=u&&(e.destroyHooks??=[]).push(t,u)}}function la(e,n,t){Fp(e,n,3,t)}function ca(e,n,t,o){(3&e[2])===t&&Fp(e,n,t,o)}function yu(e,n){let t=e[2];(3&t)===n&&(t&=16383,t+=1,e[2]=t)}function Fp(e,n,t,o){const r=o??-1,s=n.length-1;let a=0;for(let l=void 0!==o?65535&e[17]:0;l<s;l++)if("number"==typeof n[l+1]){if(a=n[l],null!=o&&a>=o)break}else n[l]<0&&(e[17]+=65536),(a<r||-1==r)&&(fM(e,t,n,l),e[17]=(4294901760&e[17])+l+2),l++}function Lp(e,n){ce(4,e,n);const t=$(null);try{n.call(e)}finally{$(t),ce(5,e,n)}}function fM(e,n,t,o){const i=t[o]<0,r=t[o+1],a=e[i?-t[o]:t[o]];i?e[2]>>14<e[17]>>16&&(3&e[2])===n&&(e[2]+=16384,Lp(a,r)):Lp(a,r)}class yr{factory;injectImpl;resolving=!1;canSeeViewProviders;multi;componentProviders;index;providerFactory;constructor(n,t,o){this.factory=n,this.canSeeViewProviders=t,this.injectImpl=o}}function Vp(e){return 3===e||4===e||6===e}function Hp(e){return 64===e.charCodeAt(0)}function pi(e,n){if(null!==n&&0!==n.length)if(null===e||0===e.length)e=n.slice();else{let t=-1;for(let o=0;o<n.length;o++){const i=n[o];"number"==typeof i?t=i:0===t||Bp(e,t,i,0,-1===t||2===t?n[++o]:null)}}return e}function Bp(e,n,t,o,i){let r=0,s=e.length;if(-1===n)s=-1;else for(;r<e.length;){const a=e[r++];if("number"==typeof a){if(a===n){s=-1;break}if(a>n){s=r-1;break}}}for(;r<e.length;){const a=e[r];if("number"==typeof a)break;if(a===t)return void(null!==i&&(e[r+1]=i));r++,null!==i&&r++}-1!==s&&(e.splice(s,0,n),r=s+1),e.splice(r++,0,t),null!==i&&e.splice(r++,0,i)}function bu(e){return-1!==e}function Cr(e){return 32767&e}function br(e,n){let t=function mM(e){return e>>16}(e),o=n;for(;t>0;)o=o[14],t--;return o}let Du=!0;function ua(e){const n=Du;return Du=e,n}let _M=0;const on={};function da(e,n){const t=$p(e,n);if(-1!==t)return t;const o=n[1];o.firstCreatePass&&(e.injectorIndex=n.length,wu(o.data,e),wu(n,null),wu(o.blueprint,null));const i=fa(e,n),r=e.injectorIndex;if(bu(i)){const s=Cr(i),a=br(i,n),l=a[1].data;for(let c=0;c<8;c++)n[r+c]=a[s+c]|l[s+c]}return n[r+8]=i,r}function wu(e,n){e.push(0,0,0,0,0,0,0,0,n)}function $p(e,n){return-1===e.injectorIndex||e.parent&&e.parent.injectorIndex===e.injectorIndex||null===n[e.injectorIndex+8]?-1:e.injectorIndex}function fa(e,n){if(e.parent&&-1!==e.parent.injectorIndex)return e.parent.injectorIndex;let t=0,o=null,i=n;for(;null!==i;){if(o=Zp(i),null===o)return-1;if(t++,i=i[14],-1!==o.injectorIndex)return o.injectorIndex|t<<16}return-1}function Eu(e,n,t){!function vM(e,n,t){let o;"string"==typeof t?o=t.charCodeAt(0)||0:t.hasOwnProperty(ir)&&(o=t[ir]),null==o&&(o=t[ir]=_M++);const i=255&o;n.data[e+(i>>5)]|=1<<i}(e,n,t)}function zp(e,n,t){if(8&t||void 0!==e)return e;xc()}function Gp(e,n,t,o){if(8&t&&void 0===o&&(o=null),!(3&t)){const i=e[9],r=ht(void 0);try{return i?i.get(n,o,8&t):xg(n,o,8&t)}finally{ht(r)}}return zp(o,0,t)}function qp(e,n,t,o=0,i){if(null!==e){if(2048&n[2]&&!(2&o)){const s=function wM(e,n,t,o,i){let r=e,s=n;for(;null!==r&&null!==s&&2048&s[2]&&!Fn(s);){const a=Wp(r,s,t,2|o,on);if(a!==on)return a;let l=r.parent;if(!l){const c=s[20];if(c){const u=c.get(t,on,o);if(u!==on)return u}l=Zp(s),s=s[14]}r=l}return i}(e,n,t,o,on);if(s!==on)return s}const r=Wp(e,n,t,o,on);if(r!==on)return r}return Gp(n,t,o,i)}function Wp(e,n,t,o,i){const r=function bM(e){if("string"==typeof e)return e.charCodeAt(0)||0;const n=e.hasOwnProperty(ir)?e[ir]:void 0;return"number"==typeof n?n>=0?255&n:DM:n}(t);if("function"==typeof r){if(!up(n,e,o))return 1&o?zp(i,0,o):Gp(n,t,o,i);try{let s;if(s=r(o),null!=s||8&o)return s;xc()}finally{gp()}}else if("number"==typeof r){let s=null,a=$p(e,n),l=-1,c=1&o?n[15][5]:null;for((-1===a||4&o)&&(l=-1===a?fa(e,n):n[a+8],-1!==l&&Jp(o,!1)?(s=n[1],a=Cr(l),n=br(l,n)):a=-1);-1!==a;){const u=n[1];if(Yp(r,a,u.data)){const d=CM(a,n,t,s,o,c);if(d!==on)return d}l=n[a+8],-1!==l&&Jp(o,n[1].data[a+8]===c)&&Yp(r,a,n)?(s=u,a=Cr(l),n=br(l,n)):a=-1}}return i}function CM(e,n,t,o,i,r){const s=n[1],a=s.data[e+8],u=ha(a,s,t,null==o?Rn(a)&&Du:o!=s&&!!(3&a.type),1&i&&r===a);return null!==u?Dr(n,s,u,a):on}function ha(e,n,t,o,i){const r=e.providerIndexes,s=n.data,a=1048575&r,l=e.directiveStart,u=r>>20,g=i?a+u:e.directiveEnd;for(let h=o?a:a+u;h<g;h++){const p=s[h];if(h<l&&t===p||h>=l&&p.type===t)return h}if(i){const h=s[l];if(h&&Tt(h)&&h.type===t)return l}return null}function Dr(e,n,t,o){let i=e[t];const r=n.data;if(i instanceof yr){const s=i;s.resolving&&Ng(function ne(e){return"function"==typeof e?e.name||e.toString():"object"==typeof e&&null!=e&&"function"==typeof e.type?e.type.name||e.type.toString():W(e)}(r[t]));const a=ua(s.canSeeViewProviders);s.resolving=!0;const u=s.injectImpl?ht(s.injectImpl):null;up(e,o,0);try{i=e[t]=s.factory(void 0,r,e,o),n.firstCreatePass&&t>=o.directiveStart&&function dM(e,n,t){const{ngOnChanges:o,ngOnInit:i,ngDoCheck:r}=n.type.prototype;if(o){const s=xp(n);(t.preOrderHooks??=[]).push(e,s),(t.preOrderCheckHooks??=[]).push(e,s)}i&&(t.preOrderHooks??=[]).push(0-e,i),r&&((t.preOrderHooks??=[]).push(e,r),(t.preOrderCheckHooks??=[]).push(e,r))}(t,r[t],n)}finally{null!==u&&ht(u),ua(a),s.resolving=!1,gp()}}return i}function Yp(e,n,t){return!!(t[n+(e>>5)]&1<<e)}function Jp(e,n){return!(2&e||1&e&&n)}class Se{_tNode;_lView;constructor(n,t){this._tNode=n,this._lView=t}get(n,t,o){return qp(this._tNode,this._lView,n,rr(o),t)}}function DM(){return new Se(K(),w())}function Ze(e){return yn(()=>{const n=e.prototype.constructor,t=n[ao]||Iu(n),o=Object.prototype;let i=Object.getPrototypeOf(e.prototype).constructor;for(;i&&i!==o;){const r=i[ao]||Iu(i);if(r&&r!==t)return r;i=Object.getPrototypeOf(i)}return r=>new r})}function Iu(e){return Us(e)?()=>{const n=Iu(Y(e));return n&&n()}:co(e)}function Zp(e){const n=e[1],t=n.type;return 2===t?n.declTNode:1===t?e[5]:null}function AM(){return mi(K(),w())}function mi(e,n){return new at(gt(e,n))}let at=(()=>class e{nativeElement;constructor(t){this.nativeElement=t}static __NG_ELEMENT_ID__=AM})();function tm(e){return e instanceof at?e.nativeElement:e}function xM(){return this._results[Symbol.iterator]()}class kM{_emitDistinctChangesOnly;dirty=!0;_onDirty=void 0;_results=[];_changesDetected=!1;_changes=void 0;length=0;first=void 0;last=void 0;get changes(){return this._changes??=new Kt}constructor(n=!1){this._emitDistinctChangesOnly=n}get(n){return this._results[n]}map(n){return this._results.map(n)}filter(n){return this._results.filter(n)}find(n){return this._results.find(n)}reduce(n,t){return this._results.reduce(n,t)}forEach(n){this._results.forEach(n)}some(n){return this._results.some(n)}toArray(){return this._results.slice()}toString(){return this._results.toString()}reset(n,t){this.dirty=!1;const o=function kt(e){return e.flat(Number.POSITIVE_INFINITY)}(n);(this._changesDetected=!function eI(e,n,t){if(e.length!==n.length)return!1;for(let o=0;o<e.length;o++){let i=e[o],r=n[o];if(t&&(i=t(i),r=t(r)),r!==i)return!1}return!0}(this._results,o,t))&&(this._results=o,this.length=o.length,this.last=o[this.length-1],this.first=o[0])}notifyOnChanges(){void 0!==this._changes&&(this._changesDetected||!this._emitDistinctChangesOnly)&&this._changes.next(this)}onDirty(n){this._onDirty=n}setDirty(){this.dirty=!0,this._onDirty?.()}destroy(){void 0!==this._changes&&(this._changes.complete(),this._changes.unsubscribe())}[Symbol.iterator]=xM}function wr(e){return!(128&~e.flags)}var pa=function(e){return e[e.OnPush=0]="OnPush",e[e.Default=1]="Default",e}(pa||{});const ma=new Map;let LM=0;function Ou(e){ma.delete(e[19])}const Au="__ngContext__";function _t(e,n){Te(n)?(e[Au]=n[19],function VM(e){ma.set(e[19],e)}(n)):e[Au]=n}function fm(e){return gm(e[12])}function hm(e){return gm(e[4])}function gm(e){for(;null!==e&&!it(e);)e=e[4];return e}let xu;function Eo(){if(void 0!==xu)return xu;if(typeof document<"u")return document;throw new O(210,!1)}const va=new R("",{providedIn:"root",factory:()=>a0}),a0="ng",bm=new R(""),ku=new R("",{providedIn:"platform",factory:()=>"unknown"}),Dm=new R("",{providedIn:"root",factory:()=>Eo().body?.querySelector("[ngCspNonce]")?.getAttribute("ngCspNonce")||null}),m0=new R("",{providedIn:"root",factory:()=>!1});function Gm(e,n){const t=e.contentQueries;if(null!==t){const o=$(null);try{for(let i=0;i<t.length;i+=2){const s=t[i+1];if(-1!==s){const a=e.data[s];sa(t[i]),a.contentQueries(2,n[s],s)}}}finally{$(o)}}}function Zu(e,n,t){sa(0);const o=$(null);try{n(e,t)}finally{$(o)}}function Qu(e,n,t){if(Zc(n)){const o=$(null);try{const r=n.directiveEnd;for(let s=n.directiveStart;s<r;s++){const a=e.data[s];a.contentQueries&&a.contentQueries(1,t[s],s)}}finally{$(o)}}}var bn=function(e){return e[e.Emulated=0]="Emulated",e[e.None=2]="None",e[e.ShadowDom=3]="ShadowDom",e}(bn||{});let Ea,Ia;function bi(e){return function qm(){if(void 0===Ea&&(Ea=null,Ee.trustedTypes))try{Ea=Ee.trustedTypes.createPolicy("angular",{createHTML:e=>e,createScript:e=>e,createScriptURL:e=>e})}catch{}return Ea}()?.createHTML(e)||e}function Wm(e){return function Ku(){if(void 0===Ia&&(Ia=null,Ee.trustedTypes))try{Ia=Ee.trustedTypes.createPolicy("angular#unsafe-bypass",{createHTML:e=>e,createScript:e=>e,createScriptURL:e=>e})}catch{}return Ia}()?.createHTML(e)||e}class Zm{changingThisBreaksApplicationSecurity;constructor(n){this.changingThisBreaksApplicationSecurity=n}toString(){return`SafeValue must use [property]=binding: ${this.changingThisBreaksApplicationSecurity} (see ${Mg})`}}function Bn(e){return e instanceof Zm?e.changingThisBreaksApplicationSecurity:e}function Tr(e,n){const t=function Y0(e){return e instanceof Zm&&e.getTypeName()||null}(e);if(null!=t&&t!==n){if("ResourceURL"===t&&"URL"===n)return!0;throw new Error(`Required a safe ${n}, got a ${t} (see ${Mg})`)}return t===n}class J0{inertDocumentHelper;constructor(n){this.inertDocumentHelper=n}getInertBodyElement(n){n="<body><remove></remove>"+n;try{const t=(new window.DOMParser).parseFromString(bi(n),"text/html").body;return null===t?this.inertDocumentHelper.getInertBodyElement(n):(t.firstChild?.remove(),t)}catch{return null}}}class Z0{defaultDoc;inertDocument;constructor(n){this.defaultDoc=n,this.inertDocument=this.defaultDoc.implementation.createHTMLDocument("sanitization-inert")}getInertBodyElement(n){const t=this.inertDocument.createElement("template");return t.innerHTML=bi(n),t}}const K0=/^(?!javascript:)(?:[a-z0-9+.-]+:|[^&:\/?#]*(?:[\/?#]|$))/i;function Xu(e){return(e=String(e)).match(K0)?e:"unsafe:"+e}function Dn(e){const n={};for(const t of e.split(","))n[t]=!0;return n}function Sr(...e){const n={};for(const t of e)for(const o in t)t.hasOwnProperty(o)&&(n[o]=!0);return n}const Km=Dn("area,br,col,hr,img,wbr"),Xm=Dn("colgroup,dd,dt,li,p,tbody,td,tfoot,th,thead,tr"),e_=Dn("rp,rt"),ed=Sr(Km,Sr(Xm,Dn("address,article,aside,blockquote,caption,center,del,details,dialog,dir,div,dl,figure,figcaption,footer,h1,h2,h3,h4,h5,h6,header,hgroup,hr,ins,main,map,menu,nav,ol,pre,section,summary,table,ul")),Sr(e_,Dn("a,abbr,acronym,audio,b,bdi,bdo,big,br,cite,code,del,dfn,em,font,i,img,ins,kbd,label,map,mark,picture,q,ruby,rp,rt,s,samp,small,source,span,strike,strong,sub,sup,time,track,tt,u,var,video")),Sr(e_,Xm)),td=Dn("background,cite,href,itemtype,longdesc,poster,src,xlink:href"),t_=Sr(td,Dn("abbr,accesskey,align,alt,autoplay,axis,bgcolor,border,cellpadding,cellspacing,class,clear,color,cols,colspan,compact,controls,coords,datetime,default,dir,download,face,headers,height,hidden,hreflang,hspace,ismap,itemscope,itemprop,kind,label,lang,language,loop,media,muted,nohref,nowrap,open,preload,rel,rev,role,rows,rowspan,rules,scope,scrolling,shape,size,sizes,span,srclang,srcset,start,summary,tabindex,target,title,translate,type,usemap,valign,value,vspace,width"),Dn("aria-activedescendant,aria-atomic,aria-autocomplete,aria-busy,aria-checked,aria-colcount,aria-colindex,aria-colspan,aria-controls,aria-current,aria-describedby,aria-details,aria-disabled,aria-dropeffect,aria-errormessage,aria-expanded,aria-flowto,aria-grabbed,aria-haspopup,aria-hidden,aria-invalid,aria-keyshortcuts,aria-label,aria-labelledby,aria-level,aria-live,aria-modal,aria-multiline,aria-multiselectable,aria-orientation,aria-owns,aria-placeholder,aria-posinset,aria-pressed,aria-readonly,aria-relevant,aria-required,aria-roledescription,aria-rowcount,aria-rowindex,aria-rowspan,aria-selected,aria-setsize,aria-sort,aria-valuemax,aria-valuemin,aria-valuenow,aria-valuetext")),X0=Dn("script,style,template");class eT{sanitizedSomething=!1;buf=[];sanitizeChildren(n){let t=n.firstChild,o=!0,i=[];for(;t;)if(t.nodeType===Node.ELEMENT_NODE?o=this.startElement(t):t.nodeType===Node.TEXT_NODE?this.chars(t.nodeValue):this.sanitizedSomething=!0,o&&t.firstChild)i.push(t),t=oT(t);else for(;t;){t.nodeType===Node.ELEMENT_NODE&&this.endElement(t);let r=nT(t);if(r){t=r;break}t=i.pop()}return this.buf.join("")}startElement(n){const t=n_(n).toLowerCase();if(!ed.hasOwnProperty(t))return this.sanitizedSomething=!0,!X0.hasOwnProperty(t);this.buf.push("<"),this.buf.push(t);const o=n.attributes;for(let i=0;i<o.length;i++){const r=o.item(i),s=r.name,a=s.toLowerCase();if(!t_.hasOwnProperty(a)){this.sanitizedSomething=!0;continue}let l=r.value;td[a]&&(l=Xu(l)),this.buf.push(" ",s,'="',i_(l),'"')}return this.buf.push(">"),!0}endElement(n){const t=n_(n).toLowerCase();ed.hasOwnProperty(t)&&!Km.hasOwnProperty(t)&&(this.buf.push("</"),this.buf.push(t),this.buf.push(">"))}chars(n){this.buf.push(i_(n))}}function nT(e){const n=e.nextSibling;if(n&&e!==n.previousSibling)throw o_(n);return n}function oT(e){const n=e.firstChild;if(n&&function tT(e,n){return(e.compareDocumentPosition(n)&Node.DOCUMENT_POSITION_CONTAINED_BY)!==Node.DOCUMENT_POSITION_CONTAINED_BY}(e,n))throw o_(n);return n}function n_(e){const n=e.nodeName;return"string"==typeof n?n:"FORM"}function o_(e){return new Error(`Failed to sanitize html because the element is clobbered: ${e.outerHTML}`)}const iT=/[\uD800-\uDBFF][\uDC00-\uDFFF]/g,rT=/([^\#-~ |!])/g;function i_(e){return e.replace(/&/g,"&amp;").replace(iT,function(n){return"&#"+(1024*(n.charCodeAt(0)-55296)+(n.charCodeAt(1)-56320)+65536)+";"}).replace(rT,function(n){return"&#"+n.charCodeAt(0)+";"}).replace(/</g,"&lt;").replace(/>/g,"&gt;")}let Ma;function nd(e){return"content"in e&&function aT(e){return e.nodeType===Node.ELEMENT_NODE&&"TEMPLATE"===e.nodeName}(e)?e.content:null}var Di=function(e){return e[e.NONE=0]="NONE",e[e.HTML=1]="HTML",e[e.STYLE=2]="STYLE",e[e.SCRIPT=3]="SCRIPT",e[e.URL=4]="URL",e[e.RESOURCE_URL=5]="RESOURCE_URL",e}(Di||{});function r_(e){const n=Or();return n?Wm(n.sanitize(Di.HTML,e)||""):Tr(e,"HTML")?Wm(Bn(e)):function sT(e,n){let t=null;try{Ma=Ma||function Qm(e){const n=new Z0(e);return function Q0(){try{return!!(new window.DOMParser).parseFromString(bi(""),"text/html")}catch{return!1}}()?new J0(n):n}(e);let o=n?String(n):"";t=Ma.getInertBodyElement(o);let i=5,r=o;do{if(0===i)throw new Error("Failed to sanitize html because the input is unstable");i--,o=r,r=t.innerHTML,t=Ma.getInertBodyElement(o)}while(o!==r);return bi((new eT).sanitizeChildren(nd(t)||t))}finally{if(t){const o=nd(t)||t;for(;o.firstChild;)o.firstChild.remove()}}}(Eo(),W(e))}function jn(e){const n=Or();return n?n.sanitize(Di.URL,e)||"":Tr(e,"URL")?Bn(e):Xu(W(e))}function Or(){const e=w();return e&&e[10].sanitizer}const gT=/^>|^->|<!--|-->|--!>|<!-$/g,pT=/(<|>)/g;function Oa(e){return e.ownerDocument.defaultView}function TT(e,n,t){let o=e.length;for(;;){const i=e.indexOf(n,t);if(-1===i)return i;if(0===i||e.charCodeAt(i-1)<=32){const r=n.length;if(i+r===o||e.charCodeAt(i+r)<=32)return i}t=i+1}}const p_="ng-template";function ST(e,n,t,o){let i=0;if(o){for(;i<n.length&&"string"==typeof n[i];i+=2)if("class"===n[i]&&-1!==TT(n[i+1].toLowerCase(),t,0))return!0}else if(rd(e))return!1;if(i=n.indexOf(1,i),i>-1){let r;for(;++i<n.length&&"string"==typeof(r=n[i]);)if(r.toLowerCase()===t)return!0}return!1}function rd(e){return 4===e.type&&e.value!==p_}function OT(e,n,t){return n===(4!==e.type||t?e.value:p_)}function NT(e,n,t){let o=4;const i=e.attrs,r=null!==i?function kT(e){for(let n=0;n<e.length;n++)if(Vp(e[n]))return n;return e.length}(i):0;let s=!1;for(let a=0;a<n.length;a++){const l=n[a];if("number"!=typeof l){if(!s)if(4&o){if(o=2|1&o,""!==l&&!OT(e,l,t)||""===l&&1===n.length){if(Gt(o))return!1;s=!0}}else if(8&o){if(null===i||!ST(e,i,l,t)){if(Gt(o))return!1;s=!0}}else{const c=n[++a],u=AT(l,i,rd(e),t);if(-1===u){if(Gt(o))return!1;s=!0;continue}if(""!==c){let d;if(d=u>r?"":i[u+1].toLowerCase(),2&o&&c!==d){if(Gt(o))return!1;s=!0}}}}else{if(!s&&!Gt(o)&&!Gt(l))return!1;if(s&&Gt(l))continue;s=!1,o=l|1&o}}return Gt(o)||s}function Gt(e){return!(1&e)}function AT(e,n,t,o){if(null===n)return-1;let i=0;if(o||!t){let r=!1;for(;i<n.length;){const s=n[i];if(s===e)return i;if(3===s||6===s)r=!0;else{if(1===s||2===s){let a=n[++i];for(;"string"==typeof a;)a=n[++i];continue}if(4===s)break;if(0===s){i+=4;continue}}i+=r?1:2}return-1}return function RT(e,n){let t=e.indexOf(4);if(t>-1)for(t++;t<e.length;){const o=e[t];if("number"==typeof o)return-1;if(o===n)return t;t++}return-1}(n,e)}function m_(e,n,t=!1){for(let o=0;o<n.length;o++)if(NT(e,n[o],t))return!0;return!1}function __(e,n){return e?":not("+n.trim()+")":n}function LT(e){let n=e[0],t=1,o=2,i="",r=!1;for(;t<e.length;){let s=e[t];if("string"==typeof s)if(2&o){const a=e[++t];i+="["+s+(a.length>0?'="'+a+'"':"")+"]"}else 8&o?i+="."+s:4&o&&(i+=" "+s);else""!==i&&!Gt(s)&&(n+=__(r,i),i=""),o=s,r=r||!Gt(o);t++}return""!==i&&(n+=__(r,i)),n}const se={};function ad(e,n){return e.createComment(function a_(e){return e.replace(gT,n=>n.replace(pT,"\u200b$1\u200b"))}(n))}function Na(e,n,t){return e.createElement(n,t)}function Mo(e,n,t,o,i){e.insertBefore(n,t,o,i)}function y_(e,n,t){e.appendChild(n,t)}function C_(e,n,t,o,i){null!==o?Mo(e,n,t,o,i):y_(e,n,t)}function Nr(e,n,t){e.removeChild(null,n,t)}function D_(e,n,t){const{mergedAttrs:o,classes:i,styles:r}=t;null!==o&&function pM(e,n,t){let o=0;for(;o<t.length;){const i=t[o];if("number"==typeof i){if(0!==i)break;o++;const r=t[o++],s=t[o++],a=t[o++];e.setAttribute(n,s,a,r)}else{const r=i,s=t[++o];Hp(r)?e.setProperty(n,r,s):e.setAttribute(n,r,s),o++}}}(e,n,o),null!==i&&function BT(e,n,t){""===t?e.removeAttribute(n,"class"):e.setAttribute(n,"class",t)}(e,n,i),null!==r&&function HT(e,n,t){e.setAttribute(n,"style",t)}(e,n,r)}function ld(e,n,t,o,i,r,s,a,l,c,u){const d=q+o,g=d+i,h=function jT(e,n){const t=[];for(let o=0;o<n;o++)t.push(o<e?null:se);return t}(d,g),p="function"==typeof c?c():c;return h[1]={type:e,blueprint:h,template:t,queries:null,viewQuery:a,declTNode:n,data:h.slice().fill(null,d),bindingStartIndex:d,expandoStartIndex:g,hostBindingOpCodes:null,firstCreatePass:!0,firstUpdatePass:!0,staticViewQueries:!1,staticContentQueries:!1,preOrderHooks:null,preOrderCheckHooks:null,contentHooks:null,contentCheckHooks:null,viewHooks:null,viewCheckHooks:null,destroyHooks:null,cleanup:null,contentQueries:null,components:null,directiveRegistry:"function"==typeof r?r():r,pipeRegistry:"function"==typeof s?s():s,firstChild:null,schemas:l,consts:p,incompleteFirstPass:!1,ssrId:u}}function Aa(e,n,t,o,i,r,s,a,l,c,u){const d=n.blueprint.slice();return d[0]=i,d[2]=1228|o,(null!==c||e&&2048&e[2])&&(d[2]|=2048),Kg(d),d[3]=d[14]=e,d[8]=t,d[10]=s||e&&e[10],d[11]=a||e&&e[11],d[9]=l||e&&e[9]||null,d[5]=r,d[19]=function PM(){return LM++}(),d[6]=u,d[20]=c,d[15]=2==n.type?e[15]:d,d}function cd(e){let n=16;return e.signals?n=4096:e.onPush&&(n=64),n}function Ar(e,n,t,o){if(0===t)return-1;const i=n.length;for(let r=0;r<t;r++)n.push(o),e.blueprint.push(o),e.data.push(null);return i}function ud(e,n){return e[12]?e[13][4]=n:e[12]=n,e[13]=n,n}function f(e=1){E_(J(),w(),qe()+e,!1)}function E_(e,n,t,o){if(!o)if(3&~n[2]){const r=e.preOrderHooks;null!==r&&ca(n,r,0,t)}else{const r=e.preOrderCheckHooks;null!==r&&la(n,r,t)}vo(t)}var xa=function(e){return e[e.None=0]="None",e[e.SignalBased=1]="SignalBased",e[e.HasDecoratorInputTransform=2]="HasDecoratorInputTransform",e}(xa||{});function xr(e,n,t,o){const i=$(null);try{const[r,s,a]=e.inputs[t];let l=null;0!==(s&xa.SignalBased)&&(l=n[r][Ye]),null!==l&&void 0!==l.transformFn?o=l.transformFn(o):null!==a&&(o=a.call(n,o)),null!==e.setInput?e.setInput(n,l,o,t,r):Ap(n,l,r,o)}finally{$(i)}}function I_(e,n,t,o,i){const r=qe(),s=2&o;try{vo(-1),s&&n.length>q&&E_(e,n,q,!1),ce(s?2:0,i,t),t(o,i)}finally{vo(r),ce(s?3:1,i,t)}}function ka(e,n,t){(function YT(e,n,t){const o=t.directiveStart,i=t.directiveEnd;Rn(t)&&function UT(e,n,t){const o=gt(n,e),i=function w_(e){const n=e.tView;return null===n||n.incompleteFirstPass?e.tView=ld(1,null,e.template,e.decls,e.vars,e.directiveDefs,e.pipeDefs,e.viewQuery,e.schemas,e.consts,e.id):n}(t),r=e[10].rendererFactory,s=ud(e,Aa(e,i,null,cd(t),o,n,null,r.createRenderer(o,t),null,null,null));e[n.index]=s}(n,t,e.data[o+t.componentOffset]),e.firstCreatePass||da(t,n);const r=t.initialInputs;for(let s=o;s<i;s++){const a=e.data[s],l=Dr(n,e,s,t);_t(l,n),null!==r&&KT(0,s-o,l,a,0,r),Tt(a)&&(rt(t.index,n)[8]=Dr(n,e,s,t))}})(e,n,t),!(64&~t.flags)&&function JT(e,n,t){const o=t.directiveStart,i=t.directiveEnd,r=t.index,s=function SI(){return z.lFrame.currentDirectiveIndex}();try{vo(r);for(let a=o;a<i;a++){const l=e.data[a],c=n[a];su(a),(null!==l.hostBindings||0!==l.hostVars||null!==l.hostAttrs)&&ZT(l,c)}}finally{vo(-1),su(s)}}(e,n,t)}function dd(e,n,t=gt){const o=n.localNames;if(null!==o){let i=n.index+1;for(let r=0;r<o.length;r+=2){const s=o[r+1],a=-1===s?t(n,e):e[s];e[i++]=a}}}let M_=()=>null;function fd(e,n,t,o,i,r){_d(e,n[1],n,t,o)?Rn(e)&&function WT(e,n){const t=rt(n,e);16&t[2]||(t[2]|=64)}(n,e.index):function hd(e,n,t,o,i,r){if(3&e.type){const s=gt(e,n);t=function qT(e){return"class"===e?"className":"for"===e?"htmlFor":"formaction"===e?"formAction":"innerHtml"===e?"innerHTML":"readonly"===e?"readOnly":"tabindex"===e?"tabIndex":e}(t),o=null!=r?r(o,e.value||"",t):o,i.setProperty(s,t,o)}}(e,n,t,o,i,r)}function ZT(e,n){null!==e.hostBindings&&e.hostBindings(1,n)}function gd(e,n){const t=e.directiveRegistry;let o=null;if(t)for(let i=0;i<t.length;i++){const r=t[i];m_(n,r.selectors,!1)&&(o??=[],Tt(r)?o.unshift(r):o.push(r))}return o}function KT(e,n,t,o,i,r){const s=r[n];if(null!==s)for(let a=0;a<s.length;a+=2)xr(o,t,s[a],s[a+1])}function _d(e,n,t,o,i){const r=e.inputs?.[o],s=e.hostDirectiveInputs?.[o];let a=!1;if(s)for(let l=0;l<s.length;l+=2){const c=s[l];xr(n.data[c],t[c],s[l+1],i),a=!0}if(r)for(const l of r)xr(n.data[l],t[l],o,i),a=!0;return a}function eS(e,n){const t=rt(n,e),o=t[1];!function tS(e,n){for(let t=n.length;t<e.blueprint.length;t++)n.push(e.blueprint[t])}(o,t);const i=t[0];null!==i&&null===t[6]&&(t[6]=null),ce(18),Ra(o,t,t[8]),ce(19,t[8])}function Ra(e,n,t){cu(n);try{const o=e.viewQuery;null!==o&&Zu(1,o,t);const i=e.template;null!==i&&I_(e,n,i,1,t),e.firstCreatePass&&(e.firstCreatePass=!1),n[18]?.finishViewCreation(e),e.staticContentQueries&&Gm(e,n),e.staticViewQueries&&Zu(2,e.viewQuery,t);const r=e.components;null!==r&&function nS(e,n){for(let t=0;t<n.length;t++)eS(e,n[t])}(n,r)}catch(o){throw e.firstCreatePass&&(e.incompleteFirstPass=!0,e.firstCreatePass=!1),o}finally{n[2]&=-5,uu()}}function Ei(e,n,t,o){const i=$(null);try{const r=n.tView,l=Aa(e,r,t,4096&e[2]?4096:16,null,n,null,null,o?.injector??null,o?.embeddedViewInjector??null,o?.dehydratedView??null);l[16]=e[n.index];const u=e[18];return null!==u&&(l[18]=u.createEmbeddedView(r)),Ra(r,l,t),l}finally{$(i)}}function To(e,n){return!n||null===n.firstChild||wr(e)}function yd(e,n){return undefined(e,n)}var Un=function(e){return e[e.Important=1]="Important",e[e.DashCase=2]="DashCase",e}(Un||{});function So(e){return!(32&~e.flags)}function Ii(e,n,t,o,i){if(null!=o){let r,s=!1;it(o)?r=o:Te(o)&&(s=!0,o=o[0]);const a=$e(o);0===e&&null!==t?null==i?y_(n,t,a):Mo(n,t,a,i||null,!0):1===e&&null!==t?Mo(n,t,a,i||null,!0):2===e?Nr(n,a,s):3===e&&n.destroyNode(a),null!=r&&function uS(e,n,t,o,i){const r=t[7];r!==$e(t)&&Ii(n,e,o,r,i);for(let a=10;a<t.length;a++){const l=t[a];La(l[1],l,e,n,o,r)}}(n,e,r,t,i)}}function A_(e,n){n[10].changeDetectionScheduler?.notify(9),La(e,n,n[11],2,null,null)}function Cd(e,n){const t=e[9],o=t.indexOf(n);t.splice(o,1)}function kr(e,n){if(gn(n))return;const t=n[11];t.destroyNode&&La(e,n,t,3,null,null),function sS(e){let n=e[12];if(!n)return bd(e[1],e);for(;n;){let t=null;if(Te(n))t=n[12];else{const o=n[10];o&&(t=o)}if(!t){for(;n&&!n[4]&&n!==e;)Te(n)&&bd(n[1],n),n=n[3];null===n&&(n=e),Te(n)&&bd(n[1],n),t=n&&n[4]}n=t}}(n)}function bd(e,n){if(gn(n))return;const t=$(null);try{n[2]&=-129,n[2]|=256,n[24]&&Rs(n[24]),function lS(e,n){let t;if(null!=e&&null!=(t=e.destroyHooks))for(let o=0;o<t.length;o+=2){const i=n[t[o]];if(!(i instanceof yr)){const r=t[o+1];if(Array.isArray(r))for(let s=0;s<r.length;s+=2){const a=i[r[s]],l=r[s+1];ce(4,a,l);try{l.call(a)}finally{ce(5,a,l)}}else{ce(4,i,r);try{r.call(i)}finally{ce(5,i,r)}}}}}(e,n),function aS(e,n){const t=e.cleanup,o=n[7];if(null!==t)for(let s=0;s<t.length-1;s+=2)if("string"==typeof t[s]){const a=t[s+3];a>=0?o[a]():o[-a].unsubscribe(),s+=2}else t[s].call(o[t[s+1]]);null!==o&&(n[7]=null);const i=n[21];if(null!==i){n[21]=null;for(let s=0;s<i.length;s++)(0,i[s])()}const r=n[23];if(null!==r){n[23]=null;for(const s of r)s.destroy()}}(e,n),1===n[1].type&&n[11].destroy();const o=n[16];if(null!==o&&it(n[3])){o!==n[3]&&Cd(o,n);const i=n[18];null!==i&&i.detachView(e)}Ou(n)}finally{$(t)}}function Dd(e,n,t){return function x_(e,n,t){let o=n;for(;null!==o&&168&o.type;)o=(n=o).parent;if(null===o)return t[0];if(Rn(o)){const{encapsulation:i}=e.data[o.directiveStart+o.componentOffset];if(i===bn.None||i===bn.Emulated)return null}return gt(o,t)}(e,n.parent,t)}let F_=function R_(e,n,t){return 40&e.type?gt(e,t):null};function Fa(e,n,t,o){const i=Dd(e,o,n),r=n[11],a=function k_(e,n,t){return F_(e,n,t)}(o.parent||n[5],o,n);if(null!=i)if(Array.isArray(t))for(let l=0;l<t.length;l++)C_(r,i,t[l],a,!1);else C_(r,i,t,a,!1)}function Oo(e,n){if(null!==n){const t=n.type;if(3&t)return gt(n,e);if(4&t)return Ed(-1,e[n.index]);if(8&t){const o=n.child;if(null!==o)return Oo(e,o);{const i=e[n.index];return it(i)?Ed(-1,i):$e(i)}}if(128&t)return Oo(e,n.next);if(32&t)return yd(n,e)()||$e(e[n.index]);{const o=P_(e,n);return null!==o?Array.isArray(o)?o[0]:Oo(pn(e[15]),o):Oo(e,n.next)}}return null}function P_(e,n){return null!==n?e[15][5].projection[n.projection]:null}function Ed(e,n){const t=10+e+1;if(t<n.length){const o=n[t],i=o[1].firstChild;if(null!==i)return Oo(o,i)}return n[7]}function Id(e,n,t,o,i,r,s){for(;null!=t;){if(128===t.type){t=t.next;continue}const a=o[t.index],l=t.type;if(s&&0===n&&(a&&_t($e(a),o),t.flags|=2),!So(t))if(8&l)Id(e,n,t.child,o,i,r,!1),Ii(n,e,i,a,r);else if(32&l){const c=yd(t,o);let u;for(;u=c();)Ii(n,e,i,u,r);Ii(n,e,i,a,r)}else 16&l?V_(e,n,o,t,i,r):Ii(n,e,i,a,r);t=s?t.projectionNext:t.next}}function La(e,n,t,o,i,r){Id(t,o,e.firstChild,n,i,r,!1)}function V_(e,n,t,o,i,r){const s=t[15],l=s[5].projection[o.projection];if(Array.isArray(l))for(let c=0;c<l.length;c++)Ii(n,e,i,l[c],r);else{let c=l;const u=s[3];wr(o)&&(c.flags|=128),Id(e,n,c,u,i,r,!0)}}function Rr(e,n,t,o,i=!1){for(;null!==t;){if(128===t.type){t=i?t.projectionNext:t.next;continue}const r=n[t.index];null!==r&&o.push($e(r)),it(r)&&fS(r,o);const s=t.type;if(8&s)Rr(e,n,t.child,o);else if(32&s){const a=yd(t,n);let l;for(;l=a();)o.push(l)}else if(16&s){const a=P_(n,t);if(Array.isArray(a))o.push(...a);else{const l=pn(n[15]);Rr(l[1],l,a,o,!0)}}t=i?t.projectionNext:t.next}return o}function fS(e,n){for(let t=10;t<e.length;t++){const o=e[t],i=o[1].firstChild;null!==i&&Rr(o[1],o,i,n)}e[7]!==e[0]&&n.push(e[7])}function H_(e){if(null!==e[25]){for(const n of e[25])n.impl.addSequence(n);e[25].length=0}}let B_=[];const mS={...Yo,consumerIsAlwaysLive:!0,kind:"template",consumerMarkedDirty:e=>{ri(e.lView)},consumerOnSignalRead(){this.lView[24]=this}},vS={...Yo,consumerIsAlwaysLive:!0,kind:"template",consumerMarkedDirty:e=>{let n=pn(e.lView);for(;n&&!j_(n[1]);)n=pn(n);n&&Xg(n)},consumerOnSignalRead(){this.lView[24]=this}};function j_(e){return 2!==e.type}function U_(e){if(null===e[23])return;let n=!0;for(;n;){let t=!1;for(const o of e[23])o.dirty&&(t=!0,null===o.zone||Zone.current===o.zone?o.run():o.zone.run(()=>o.run()));n=t&&!!(8192&e[2])}}function Pa(e,n=0){const o=e[10].rendererFactory;o.begin?.();try{!function CS(e,n){const t=ap();try{ra(!0),Md(e,n);let o=0;for(;oa(e);){if(100===o)throw new O(103,!1);o++,Md(e,1)}}finally{ra(t)}}(e,n)}finally{o.end?.()}}function $_(e,n,t,o){if(gn(n))return;const i=n[2];cu(n);let a=!0,l=null,c=null;j_(e)?(c=function hS(e){return e[24]??function gS(e){const n=B_.pop()??Object.create(mS);return n.lView=e,n}(e)}(n),l=Jo(c)):null===function mc(){return Pe}()?(a=!1,c=function _S(e){const n=e[24]??Object.create(vS);return n.lView=e,n}(n),l=Jo(c)):n[24]&&(Rs(n[24]),n[24]=null);try{Kg(n),function lp(e){return z.lFrame.bindingIndex=e}(e.bindingStartIndex),null!==t&&I_(e,n,t,2,o);const u=!(3&~i);if(u){const h=e.preOrderCheckHooks;null!==h&&la(n,h,null)}else{const h=e.preOrderHooks;null!==h&&ca(n,h,0,null),yu(n,0)}if(function DS(e){for(let n=fm(e);null!==n;n=hm(n)){if(!(2&n[2]))continue;const t=n[9];for(let o=0;o<t.length;o++)Xg(t[o])}}(n),U_(n),z_(n,0),null!==e.contentQueries&&Gm(e,n),u){const h=e.contentCheckHooks;null!==h&&la(n,h)}else{const h=e.contentHooks;null!==h&&ca(n,h,1),yu(n,1)}!function ES(e,n){const t=e.hostBindingOpCodes;if(null!==t)try{for(let o=0;o<t.length;o++){const i=t[o];if(i<0)vo(~i);else{const r=i,s=t[++o],a=t[++o];TI(s,r);const l=n[r];ce(24,l),a(2,l),ce(25,l)}}}finally{vo(-1)}}(e,n);const d=e.components;null!==d&&q_(n,d,0);const g=e.viewQuery;if(null!==g&&Zu(2,g,o),u){const h=e.viewCheckHooks;null!==h&&la(n,h)}else{const h=e.viewHooks;null!==h&&ca(n,h,2),yu(n,2)}if(!0===e.firstUpdatePass&&(e.firstUpdatePass=!1),n[22]){for(const h of n[22])h();n[22]=null}H_(n),n[2]&=-73}catch(u){throw ri(n),u}finally{null!==c&&(er(c,l),a&&function pS(e){e.lView[24]!==e&&(e.lView=null,B_.push(e))}(c)),uu()}}function z_(e,n){for(let t=fm(e);null!==t;t=hm(t))for(let o=10;o<t.length;o++)G_(t[o],n)}function wS(e,n,t){ce(18);const o=rt(n,e);G_(o,t),ce(19,o[8])}function G_(e,n){Kc(e)&&Md(e,n)}function Md(e,n){const o=e[1],i=e[2],r=e[24];let s=!!(0===n&&16&i);if(s||=!!(64&i&&0===n),s||=!!(1024&i),s||=!(!r?.dirty||!ks(r)),s||=!1,r&&(r.dirty=!1),e[2]&=-9217,s)$_(o,e,o.template,e[8]);else if(8192&i){const a=$(null);try{U_(e),z_(e,1);const l=o.components;null!==l&&q_(e,l,1),H_(e)}finally{$(a)}}}function q_(e,n,t){for(let o=0;o<n.length;o++)wS(e,n[o],t)}function Fr(e,n){const t=ap()?64:1088;for(e[10].changeDetectionScheduler?.notify(n);e;){e[2]|=t;const o=pn(e);if(Fn(e)&&!o)return e;e=o}return null}function W_(e,n,t,o){return[e,!0,0,n,null,o,null,t,null,null]}function Y_(e,n){const t=10+n;if(t<e.length)return e[t]}function Mi(e,n,t,o=!0){const i=n[1];if(function IS(e,n,t,o){const i=10+o,r=t.length;o>0&&(t[i-1][4]=n),o<r-10?(n[4]=t[i],Fg(t,10+o,n)):(t.push(n),n[4]=null),n[3]=t;const s=n[16];null!==s&&t!==s&&J_(s,n);const a=n[18];null!==a&&a.insertView(e),Xc(n),n[2]|=128}(i,n,e,t),o){const s=Ed(t,e),a=n[11],l=a.parentNode(e[7]);null!==l&&function rS(e,n,t,o,i,r){o[0]=i,o[5]=n,La(e,o,t,1,i,r)}(i,e[5],a,n,l,s)}const r=n[6];null!==r&&null!==r.firstChild&&(r.firstChild=null)}function Td(e,n){const t=Lr(e,n);return void 0!==t&&kr(t[1],t),t}function Lr(e,n){if(e.length<=10)return;const t=10+n,o=e[t];if(o){const i=o[16];null!==i&&i!==e&&Cd(i,o),n>0&&(e[t-1][4]=o[4]);const r=Ws(e,10+n);!function N_(e,n){A_(e,n),n[0]=null,n[5]=null}(o[1],o);const s=r[18];null!==s&&s.detachView(r[1]),o[3]=null,o[4]=null,o[2]&=-129}return o}function J_(e,n){const t=e[9],o=n[3];(Te(o)||n[15]!==o[3][15])&&(e[2]|=2),null===t?e[9]=[n]:t.push(n)}class Pr{_lView;_cdRefInjectingView;_appRef=null;_attachedToViewContainer=!1;exhaustive;get rootNodes(){const n=this._lView,t=n[1];return Rr(t,n,t.firstChild,[])}constructor(n,t){this._lView=n,this._cdRefInjectingView=t}get context(){return this._lView[8]}set context(n){this._lView[8]=n}get destroyed(){return gn(this._lView)}destroy(){if(this._appRef)this._appRef.detachView(this);else if(this._attachedToViewContainer){const n=this._lView[3];if(it(n)){const t=n[8],o=t?t.indexOf(this):-1;o>-1&&(Lr(n,o),Ws(t,o))}this._attachedToViewContainer=!1}kr(this._lView[1],this._lView)}onDestroy(n){ia(this._lView,n)}markForCheck(){Fr(this._cdRefInjectingView||this._lView,4)}detach(){this._lView[2]&=-129}reattach(){Xc(this._lView),this._lView[2]|=128}detectChanges(){this._lView[2]|=1024,Pa(this._lView)}checkNoChanges(){}attachToViewContainerRef(){if(this._appRef)throw new O(902,!1);this._attachedToViewContainer=!0}detachFromAppRef(){this._appRef=null;const n=Fn(this._lView),t=this._lView[16];null!==t&&!n&&Cd(t,this._lView),A_(this._lView[1],this._lView)}attachToAppRef(n){if(this._attachedToViewContainer)throw new O(902,!1);this._appRef=n;const t=Fn(this._lView),o=this._lView[16];null!==o&&!t&&J_(o,this._lView),Xc(this._lView)}}let wn=(()=>class e{_declarationLView;_declarationTContainer;elementRef;static __NG_ELEMENT_ID__=MS;constructor(t,o,i){this._declarationLView=t,this._declarationTContainer=o,this.elementRef=i}get ssrId(){return this._declarationTContainer.tView?.ssrId||null}createEmbeddedView(t,o){return this.createEmbeddedViewImpl(t,o)}createEmbeddedViewImpl(t,o,i){const r=Ei(this._declarationLView,this._declarationTContainer,t,{embeddedViewInjector:o,dehydratedView:i});return new Pr(r)}})();function MS(){return Va(K(),w())}function Va(e,n){return 4&e.type?new wn(n,e,mi(e,n)):null}function Ti(e,n,t,o,i){let r=e.data[n];if(null===r)r=function Ad(e,n,t,o,i){const r=ip(),s=iu(),l=e.data[n]=function FS(e,n,t,o,i,r){let s=n?n.injectorIndex:-1,a=0;return function op(){return null!==z.skipHydrationRootTNode}()&&(a|=128),{type:t,index:o,insertBeforeIndex:null,injectorIndex:s,directiveStart:-1,directiveEnd:-1,directiveStylingLast:-1,componentOffset:-1,propertyBindings:null,flags:a,providerIndexes:0,value:i,attrs:r,mergedAttrs:null,localNames:null,initialInputs:null,inputs:null,hostDirectiveInputs:null,outputs:null,hostDirectiveOutputs:null,directiveToIndex:null,tView:null,next:null,prev:null,projectionNext:null,child:null,parent:n,projection:null,styles:null,stylesWithoutHost:null,residualStyles:void 0,classes:null,classesWithoutHost:null,residualClasses:void 0,classBindings:0,styleBindings:0}}(0,s?r:r&&r.parent,t,n,o,i);return function RS(e,n,t,o){null===e.firstChild&&(e.firstChild=n),null!==t&&(o?null==t.child&&null!==n.parent&&(t.child=n):null===t.next&&(t.next=n,n.prev=t))}(e,l,r,s),l}(e,n,t,o,i),function MI(){return z.lFrame.inI18n}()&&(r.flags|=32);else if(64&r.type){r.type=t,r.value=o,r.attrs=i;const s=function fr(){const e=z.lFrame,n=e.currentTNode;return e.isParent?n:n.parent}();r.injectorIndex=null===s?-1:s.injectorIndex}return zt(r,!0),r}function gv(e,n){let t=0,o=e.firstChild;if(o){const i=e.data.r;for(;t<i;){const r=o.nextSibling;Nr(n,o,!1),o=r,t++}}}let _O=class{},Cv=class{};class vO{resolveComponentFactory(n){throw new O(917,!1)}}let qa=class{static NULL=new vO};class Vd{}let qt=(()=>class e{destroyNode=null;static __NG_ELEMENT_ID__=()=>function yO(){const e=w(),t=rt(K().index,e);return(Te(t)?t:e)[11]}()})(),CO=(()=>{class e{static \u0275prov=X({token:e,providedIn:"root",factory:()=>null})}return e})();const Bd={};class Ni{injector;parentInjector;constructor(n,t){this.injector=n,this.parentInjector=t}get(n,t,o){const i=this.injector.get(n,Bd,o);return i!==Bd||t===Bd?i:this.parentInjector.get(n,t,o)}}function jd(e,n,t){let o=t?e.styles:null,i=t?e.classes:null,r=0;if(null!==n)for(let s=0;s<n.length;s++){const a=n[s];"number"==typeof a?r=a:1==r?i=Tc(i,a):2==r&&(o=Tc(o,a+": "+n[++s]+";"))}t?e.styles=o:e.stylesWithoutHost=o,t?e.classes=i:e.classesWithoutHost=i}function T(e,n=0){const t=w();return null===t?te(e,n):qp(K(),t,Y(e),n)}function Ud(e,n,t,o,i){const r=null===o?null:{"":-1},s=i(e,t);if(null!==s){let a=s,l=null,c=null;for(const u of s)if(null!==u.resolveHostDirectives){[a,l,c]=u.resolveHostDirectives(s);break}!function TO(e,n,t,o,i,r,s){const a=o.length;let l=!1;for(let g=0;g<a;g++){const h=o[g];!l&&Tt(h)&&(l=!0,MO(e,t,g)),Eu(da(t,n),e,h.type)}!function kO(e,n,t){e.flags|=1,e.directiveStart=n,e.directiveEnd=n+t,e.providerIndexes=n}(t,e.data.length,a);for(let g=0;g<a;g++){const h=o[g];h.providersResolver&&h.providersResolver(h)}let c=!1,u=!1,d=Ar(e,n,a,null);a>0&&(t.directiveToIndex=new Map);for(let g=0;g<a;g++){const h=o[g];if(t.mergedAttrs=pi(t.mergedAttrs,h.hostAttrs),OO(e,t,n,d,h),xO(d,h,i),null!==s&&s.has(h)){const[b,M]=s.get(h);t.directiveToIndex.set(h.type,[d,b+t.directiveStart,M+t.directiveStart])}else(null===r||!r.has(h))&&t.directiveToIndex.set(h.type,d);null!==h.contentQueries&&(t.flags|=4),(null!==h.hostBindings||null!==h.hostAttrs||0!==h.hostVars)&&(t.flags|=64);const p=h.type.prototype;!c&&(p.ngOnChanges||p.ngOnInit||p.ngDoCheck)&&((e.preOrderHooks??=[]).push(t.index),c=!0),!u&&(p.ngOnChanges||p.ngDoCheck)&&((e.preOrderCheckHooks??=[]).push(t.index),u=!0),d++}!function SO(e,n,t){for(let o=n.directiveStart;o<n.directiveEnd;o++){const i=e.data[o];if(null!==t&&t.has(i)){const r=t.get(i);wv(0,n,r,o),wv(1,n,r,o),Iv(n,o,!0)}else Dv(0,n,i,o),Dv(1,n,i,o),Iv(n,o,!1)}}(e,t,r)}(e,n,t,a,r,l,c)}null!==r&&null!==o&&function IO(e,n,t){const o=e.localNames=[];for(let i=0;i<n.length;i+=2){const r=t[n[i+1]];if(null==r)throw new O(-301,!1);o.push(n[i],r)}}(t,o,r)}function MO(e,n,t){n.componentOffset=t,(e.components??=[]).push(n.index)}function Dv(e,n,t,o){const i=0===e?t.inputs:t.outputs;for(const r in i)if(i.hasOwnProperty(r)){let s;s=0===e?n.inputs??={}:n.outputs??={},s[r]??=[],s[r].push(o),Ev(n,r)}}function wv(e,n,t,o){const i=0===e?t.inputs:t.outputs;for(const r in i)if(i.hasOwnProperty(r)){const s=i[r];let a;a=0===e?n.hostDirectiveInputs??={}:n.hostDirectiveOutputs??={},a[s]??=[],a[s].push(o,r),Ev(n,s)}}function Ev(e,n){"class"===n?e.flags|=8:"style"===n&&(e.flags|=16)}function Iv(e,n,t){const{attrs:o,inputs:i,hostDirectiveInputs:r}=e;if(null===o||!t&&null===i||t&&null===r||rd(e))return e.initialInputs??=[],void e.initialInputs.push(null);let s=null,a=0;for(;a<o.length;){const l=o[a];if(0!==l)if(5!==l){if("number"==typeof l)break;if(!t&&i.hasOwnProperty(l)){const c=i[l];for(const u of c)if(u===n){s??=[],s.push(l,o[a+1]);break}}else if(t&&r.hasOwnProperty(l)){const c=r[l];for(let u=0;u<c.length;u+=2)if(c[u]===n){s??=[],s.push(c[u+1],o[a+1]);break}}a+=2}else a+=2;else a+=4}e.initialInputs??=[],e.initialInputs.push(s)}function OO(e,n,t,o,i){e.data[o]=i;const r=i.factory||(i.factory=co(i.type)),s=new yr(r,Tt(i),T);e.blueprint[o]=s,t[o]=s,function NO(e,n,t,o,i){const r=i.hostBindings;if(r){let s=e.hostBindingOpCodes;null===s&&(s=e.hostBindingOpCodes=[]);const a=~n.index;(function AO(e){let n=e.length;for(;n>0;){const t=e[--n];if("number"==typeof t&&t<0)return t}return 0})(s)!=a&&s.push(a),s.push(t,o,r)}}(e,n,o,Ar(e,t,i.hostVars,se),i)}function xO(e,n,t){if(t){if(n.exportAs)for(let o=0;o<n.exportAs.length;o++)t[n.exportAs[o]]=e;Tt(n)&&(t[""]=e)}}function Mv(e,n,t,o,i,r,s,a){const l=n.consts,u=Ti(n,e,2,o,pt(l,s));return r&&Ud(n,t,u,pt(l,a),i),u.mergedAttrs=pi(u.mergedAttrs,u.attrs),null!==u.attrs&&jd(u,u.attrs,!1),null!==u.mergedAttrs&&jd(u,u.mergedAttrs,!0),null!==n.queries&&n.queries.elementStart(n,u),u}function Tv(e,n){vu(e,n),Zc(n)&&e.queries.elementEnd(n)}function Ya(e){return!!$d(e)&&(Array.isArray(e)||!(e instanceof Map)&&Symbol.iterator in e)}function $d(e){return null!==e&&("function"==typeof e||"object"==typeof e)}function sn(e,n,t){return e[n]=t}function De(e,n,t){return t!==se&&(!Object.is(e[n],t)&&(e[n]=t,!0))}function Ao(e,n,t,o){const i=De(e,n,t);return De(e,n+1,o)||i}function Za(e,n,t){return function o(i){Fr(Rn(e)?rt(e.index,n):n,5);const s=n[8];let a=Sv(n,s,t,i),l=o.__ngNextListenerFn__;for(;l;)a=Sv(n,s,l,i)&&a,l=l.__ngNextListenerFn__;return a}}function Sv(e,n,t,o){const i=$(null);try{return ce(6,n,t),!1!==t(o)}catch(r){return function md(e,n){const t=e[9];t&&t.get(vn,null)?.(n)}(e,r),!1}finally{ce(7,n,t),$(i)}}function Ov(e,n,t,o,i,r,s){const a=n.firstCreatePass?np(n):null,l=tp(t),c=l.length;l.push(i,r),a&&a.push(o,e,c,(c+1)*(s?-1:1))}function Qa(e,n,t,o,i,r){const a=n[1],d=n[t][a.data[t].outputs[o]].subscribe(r);Ov(e.index,a,n,i,r,d,!0)}const zn=Symbol("BINDING");class Nv extends qa{ngModule;constructor(n){super(),this.ngModule=n}resolveComponentFactory(n){const t=ie(n);return new zd(t,this.ngModule)}}class zd extends Cv{componentDef;ngModule;selector;componentType;ngContentSelectors;isBoundToModule;cachedInputs=null;cachedOutputs=null;get inputs(){return this.cachedInputs??=function qO(e){return Object.keys(e).map(n=>{const[t,o,i]=e[n],r={propName:t,templateName:n,isSignal:0!==(o&xa.SignalBased)};return i&&(r.transform=i),r})}(this.componentDef.inputs),this.cachedInputs}get outputs(){return this.cachedOutputs??=function WO(e){return Object.keys(e).map(n=>({propName:e[n],templateName:n}))}(this.componentDef.outputs),this.cachedOutputs}constructor(n,t){super(),this.componentDef=n,this.ngModule=t,this.componentType=n.type,this.selector=function PT(e){return e.map(LT).join(",")}(n.selectors),this.ngContentSelectors=n.ngContentSelectors??[],this.isBoundToModule=!!t}create(n,t,o,i,r,s){ce(22);const a=$(null);try{const l=this.componentDef,c=function QO(e,n,t,o){const i=e?["ng-version","20.0.3"]:function VT(e){const n=[],t=[];let o=1,i=2;for(;o<e.length;){let r=e[o];if("string"==typeof r)2===i?""!==r&&n.push(r,e[++o]):8===i&&t.push(r);else{if(!Gt(i))break;i=r}o++}return t.length&&n.push(1,...t),n}(n.selectors[0]);let r=null,s=null,a=0;if(t)for(const u of t)a+=u[zn].requiredVars,u.create&&(u.targetIdx=0,(r??=[]).push(u)),u.update&&(u.targetIdx=0,(s??=[]).push(u));if(o)for(let u=0;u<o.length;u++){const d=o[u];if("function"!=typeof d)for(const g of d.bindings){a+=g[zn].requiredVars;const h=u+1;g.create&&(g.targetIdx=h,(r??=[]).push(g)),g.update&&(g.targetIdx=h,(s??=[]).push(g))}}const l=[n];if(o)for(const u of o){const g=ot("function"==typeof u?u:u.type);l.push(g)}return ld(0,null,function KO(e,n){return e||n?t=>{if(1&t&&e)for(const o of e)o.create();if(2&t&&n)for(const o of n)o.update()}:null}(r,s),1,a,l,null,null,null,[i],null)}(o,l,s,r),u=function YO(e,n,t){let o=n instanceof Rt?n:n?.injector;return o&&null!==e.getStandaloneInjector&&(o=e.getStandaloneInjector(o)||o),o?new Ni(t,o):t}(l,i||this.ngModule,n),d=function JO(e){const n=e.get(Vd,null);if(null===n)throw new O(407,!1);return{rendererFactory:n,sanitizer:e.get(CO,null),changeDetectionScheduler:e.get(li,null),ngReflect:!1}}(u),g=d.rendererFactory.createRenderer(null,l),h=o?function $T(e,n,t,o){const r=o.get(m0,!1)||t===bn.ShadowDom,s=e.selectRootElement(n,r);return function zT(e){M_(e)}(s),s}(g,o,l.encapsulation,u):function ZO(e,n){const t=(e.selectors[0][0]||"div").toLowerCase();return Na(n,t,"svg"===t?"svg":"math"===t?"math":null)}(l,g),p=s?.some(Av)||r?.some(N=>"function"!=typeof N&&N.bindings.some(Av)),b=Aa(null,c,null,512|cd(l),null,null,d,g,u,null,null);b[q]=h,cu(b);let M=null;try{const N=Mv(q,c,b,"#host",()=>c.directiveRegistry,!0,0);h&&(D_(g,h,N),_t(h,b)),ka(c,b,N),Qu(c,N,b),Tv(c,N),void 0!==t&&function eN(e,n,t){const o=e.projection=[];for(let i=0;i<n.length;i++){const r=t[i];o.push(null!=r&&r.length?Array.from(r):null)}}(N,this.ngContentSelectors,t),M=rt(N.index,b),b[8]=M[8],Ra(c,b,null)}catch(N){throw null!==M&&Ou(M),Ou(b),N}finally{ce(23),uu()}return new XO(this.componentType,b,!!p)}finally{$(a)}}}function Av(e){const n=e[zn].kind;return"input"===n||"twoWay"===n}class XO extends _O{_rootLView;_hasInputBindings;instance;hostView;changeDetectorRef;componentType;location;previousInputValues=null;_tNode;constructor(n,t,o){super(),this._rootLView=t,this._hasInputBindings=o,this._tNode=ii(t[1],q),this.location=mi(this._tNode,t),this.instance=rt(this._tNode.index,t)[8],this.hostView=this.changeDetectorRef=new Pr(t,void 0),this.componentType=n}setInput(n,t){const o=this._tNode;if(this.previousInputValues??=new Map,this.previousInputValues.has(n)&&Object.is(this.previousInputValues.get(n),t))return;const i=this._rootLView;_d(o,i[1],i,n,t),this.previousInputValues.set(n,t),Fr(rt(o.index,i),1)}get injector(){return new Se(this._tNode,this._rootLView)}destroy(){this.hostView.destroy()}onDestroy(n){this.hostView.onDestroy(n)}}let an=(()=>class e{static __NG_ELEMENT_ID__=tN})();function tN(){return Rv(K(),w())}const nN=an,xv=class extends nN{_lContainer;_hostTNode;_hostLView;constructor(n,t,o){super(),this._lContainer=n,this._hostTNode=t,this._hostLView=o}get element(){return mi(this._hostTNode,this._hostLView)}get injector(){return new Se(this._hostTNode,this._hostLView)}get parentInjector(){const n=fa(this._hostTNode,this._hostLView);if(bu(n)){const t=br(n,this._hostLView),o=Cr(n);return new Se(t[1].data[o+8],t)}return new Se(null,this._hostLView)}clear(){for(;this.length>0;)this.remove(this.length-1)}get(n){const t=kv(this._lContainer);return null!==t&&t[n]||null}get length(){return this._lContainer.length-10}createEmbeddedView(n,t,o){let i,r;"number"==typeof o?i=o:null!=o&&(i=o.index,r=o.injector);const a=n.createEmbeddedViewImpl(t||{},r,null);return this.insertImpl(a,i,To(this._hostTNode,null)),a}createComponent(n,t,o,i,r,s,a){const l=n&&!function vr(e){return"function"==typeof e}(n);let c;if(l)c=t;else{const M=t||{};c=M.index,o=M.injector,i=M.projectableNodes,r=M.environmentInjector||M.ngModuleRef,s=M.directives,a=M.bindings}const u=l?n:new zd(ie(n)),d=o||this.parentInjector;if(!r&&null==u.ngModule){const N=(l?d:this.parentInjector).get(Rt,null);N&&(r=N)}ie(u.componentType??{});const b=u.create(d,i,null,r,s,a);return this.insertImpl(b.hostView,c,To(this._hostTNode,null)),b}insert(n,t){return this.insertImpl(n,t,!0)}insertImpl(n,t,o){const i=n._lView;if(function _I(e){return it(e[3])}(i)){const a=this.indexOf(n);if(-1!==a)this.detach(a);else{const l=i[3],c=new xv(l,l[5],l[3]);c.detach(c.indexOf(n))}}const r=this._adjustIndex(t),s=this._lContainer;return Mi(s,i,r,o),n.attachToViewContainerRef(),Fg(Gd(s),r,n),n}move(n,t){return this.insert(n,t)}indexOf(n){const t=kv(this._lContainer);return null!==t?t.indexOf(n):-1}remove(n){const t=this._adjustIndex(n,-1),o=Lr(this._lContainer,t);o&&(Ws(Gd(this._lContainer),t),kr(o[1],o))}detach(n){const t=this._adjustIndex(n,-1),o=Lr(this._lContainer,t);return o&&null!=Ws(Gd(this._lContainer),t)?new Pr(o):null}_adjustIndex(n,t=0){return n??this.length+t}};function kv(e){return e[8]}function Gd(e){return e[8]||(e[8]=[])}function Rv(e,n){let t;const o=n[e.index];return it(o)?t=o:(t=W_(o,n,null,e),n[e.index]=t,ud(n,t)),Fv(t,n,e,o),new xv(t,e,n)}let Fv=function Pv(e,n,t,o){if(e[7])return;let i;i=8&t.type?$e(o):function oN(e,n){const t=e[11],o=t.createComment(""),i=gt(n,e),r=t.parentNode(i);return Mo(t,r,o,t.nextSibling(i),!1),o}(n,t),e[7]=i};class Wd{queryList;matches=null;constructor(n){this.queryList=n}clone(){return new Wd(this.queryList)}setDirty(){this.queryList.setDirty()}}class Yd{queries;constructor(n=[]){this.queries=n}createEmbeddedView(n){const t=n.queries;if(null!==t){const o=null!==n.contentQueries?n.contentQueries[0]:t.length,i=[];for(let r=0;r<o;r++){const s=t.getByIndex(r);i.push(this.queries[s.indexInDeclarationView].clone())}return new Yd(i)}return null}insertView(n){this.dirtyQueriesWithMatches(n)}detachView(n){this.dirtyQueriesWithMatches(n)}finishViewCreation(n){this.dirtyQueriesWithMatches(n)}dirtyQueriesWithMatches(n){for(let t=0;t<this.queries.length;t++)null!==Xd(n,t).matches&&this.queries[t].setDirty()}}class Vv{flags;read;predicate;constructor(n,t,o=null){this.flags=t,this.read=o,this.predicate="string"==typeof n?function uN(e){return e.split(",").map(n=>n.trim())}(n):n}}class Jd{queries;constructor(n=[]){this.queries=n}elementStart(n,t){for(let o=0;o<this.queries.length;o++)this.queries[o].elementStart(n,t)}elementEnd(n){for(let t=0;t<this.queries.length;t++)this.queries[t].elementEnd(n)}embeddedTView(n){let t=null;for(let o=0;o<this.length;o++){const i=null!==t?t.length:0,r=this.getByIndex(o).embeddedTView(n,i);r&&(r.indexInDeclarationView=o,null!==t?t.push(r):t=[r])}return null!==t?new Jd(t):null}template(n,t){for(let o=0;o<this.queries.length;o++)this.queries[o].template(n,t)}getByIndex(n){return this.queries[n]}get length(){return this.queries.length}track(n){this.queries.push(n)}}class Zd{metadata;matches=null;indexInDeclarationView=-1;crossesNgTemplate=!1;_declarationNodeIndex;_appliesToNextNode=!0;constructor(n,t=-1){this.metadata=n,this._declarationNodeIndex=t}elementStart(n,t){this.isApplyingToNode(t)&&this.matchTNode(n,t)}elementEnd(n){this._declarationNodeIndex===n.index&&(this._appliesToNextNode=!1)}template(n,t){this.elementStart(n,t)}embeddedTView(n,t){return this.isApplyingToNode(n)?(this.crossesNgTemplate=!0,this.addMatch(-n.index,t),new Zd(this.metadata)):null}isApplyingToNode(n){if(this._appliesToNextNode&&1&~this.metadata.flags){const t=this._declarationNodeIndex;let o=n.parent;for(;null!==o&&8&o.type&&o.index!==t;)o=o.parent;return t===(null!==o?o.index:-1)}return this._appliesToNextNode}matchTNode(n,t){const o=this.metadata.predicate;if(Array.isArray(o))for(let i=0;i<o.length;i++){const r=o[i];this.matchTNodeWithReadOption(n,t,sN(t,r)),this.matchTNodeWithReadOption(n,t,ha(t,n,r,!1,!1))}else o===wn?4&t.type&&this.matchTNodeWithReadOption(n,t,-1):this.matchTNodeWithReadOption(n,t,ha(t,n,o,!1,!1))}matchTNodeWithReadOption(n,t,o){if(null!==o){const i=this.metadata.read;if(null!==i)if(i===at||i===an||i===wn&&4&t.type)this.addMatch(t.index,-2);else{const r=ha(t,n,i,!1,!1);null!==r&&this.addMatch(t.index,r)}else this.addMatch(t.index,o)}}addMatch(n,t){null===this.matches?this.matches=[n,t]:this.matches.push(n,t)}}function sN(e,n){const t=e.localNames;if(null!==t)for(let o=0;o<t.length;o+=2)if(t[o]===n)return t[o+1];return null}function lN(e,n,t,o){return-1===t?function aN(e,n){return 11&e.type?mi(e,n):4&e.type?Va(e,n):null}(n,e):-2===t?function cN(e,n,t){return t===at?mi(n,e):t===wn?Va(n,e):t===an?Rv(n,e):void 0}(e,n,o):Dr(e,e[1],t,n)}function Hv(e,n,t,o){const i=n[18].queries[o];if(null===i.matches){const r=e.data,s=t.matches,a=[];for(let l=0;null!==s&&l<s.length;l+=2){const c=s[l];a.push(c<0?null:lN(n,r[c],s[l+1],t.metadata.read))}i.matches=a}return i.matches}function Qd(e,n,t,o){const i=e.queries.getByIndex(t),r=i.matches;if(null!==r){const s=Hv(e,n,i,t);for(let a=0;a<r.length;a+=2){const l=r[a];if(l>0)o.push(s[a/2]);else{const c=r[a+1],u=n[-l];for(let d=10;d<u.length;d++){const g=u[d];g[16]===g[3]&&Qd(g[1],g,c,o)}if(null!==u[9]){const d=u[9];for(let g=0;g<d.length;g++){const h=d[g];Qd(h[1],h,c,o)}}}}}return o}function Bv(e,n,t){const o=new kM(!(4&~t));return function vI(e,n,t,o){const i=tp(n);i.push(t),e.firstCreatePass&&np(e).push(o,i.length-1)}(e,n,o,o.destroy),(n[18]??=new Yd).queries.push(new Wd(o))-1}function $v(e,n,t){null===e.queries&&(e.queries=new Jd),e.queries.track(new Zd(n,t))}function Xd(e,n){return e.queries.getByIndex(n)}function zv(e,n){const t=e[1],o=Xd(t,n);return o.crossesNgTemplate?Qd(t,e,n,[]):Hv(t,e,o,n)}const Jv=new Set;function Ht(e){Jv.has(e)||(Jv.add(e),performance?.mark?.("mark_feature_usage",{detail:{feature:e}}))}let xo=class{},bN=class{};class nf extends xo{ngModuleType;_parent;_bootstrapComponents=[];_r3Injector;instance;destroyCbs=[];componentFactoryResolver=new Nv(this);constructor(n,t,o,i=!0){super(),this.ngModuleType=n,this._parent=t;const r=function en(e){return e[Sg]||null}(n);this._bootstrapComponents=function rn(e){return e instanceof Function?e():e}(r.bootstrap),this._r3Injector=vp(n,t,[{provide:xo,useValue:this},{provide:qa,useValue:this.componentFactoryResolver},...o],Ge(n),new Set(["environment"])),i&&this.resolveInjectorInitializers()}resolveInjectorInitializers(){this._r3Injector.resolveInjectorInitializers(),this.instance=this._r3Injector.get(this.ngModuleType)}get injector(){return this._r3Injector}destroy(){const n=this._r3Injector;!n.destroyed&&n.destroy(),this.destroyCbs.forEach(t=>t()),this.destroyCbs=null}onDestroy(n){this.destroyCbs.push(n)}}class Zv extends bN{moduleType;constructor(n){super(),this.moduleType=n}create(n){return new nf(this.moduleType,n,[])}}class EN extends xo{injector;componentFactoryResolver=new Nv(this);instance=null;constructor(n){super();const t=new fo([...n.providers,{provide:xo,useValue:this},{provide:qa,useValue:this.componentFactoryResolver}],n.parent||zc(),n.debugName,new Set(["environment"]));this.injector=t,n.runEnvironmentInitializers&&t.resolveInjectorInitializers()}destroy(){this.injector.destroy()}onDestroy(n){this.injector.onDestroy(n)}}let IN=(()=>{class e{_injector;cachedInjectors=new Map;constructor(t){this._injector=t}getOrCreateStandaloneInjector(t){if(!t.standalone)return null;if(!this.cachedInjectors.has(t)){const o=Hc(0,t.type),i=o.length>0?function Qv(e,n,t=null){return new EN({providers:e,parent:n,debugName:t,runEnvironmentInitializers:!0}).injector}([o],this._injector,`Standalone[${t.type.name}]`):null;this.cachedInjectors.set(t,i)}return this.cachedInjectors.get(t)}ngOnDestroy(){try{for(const t of this.cachedInjectors.values())null!==t&&t.destroy()}finally{this.cachedInjectors.clear()}}static \u0275prov=X({token:e,providedIn:"environment",factory:()=>new e(te(Rt))})}return e})();function Wt(e){return yn(()=>{const n=Kv(e),t={...n,decls:e.decls,vars:e.vars,template:e.template,consts:e.consts||null,ngContentSelectors:e.ngContentSelectors,onPush:e.changeDetection===pa.OnPush,directiveDefs:null,pipeDefs:null,dependencies:n.standalone&&e.dependencies||null,getStandaloneInjector:n.standalone?i=>i.get(IN).getOrCreateStandaloneInjector(t):null,getExternalStyles:null,signals:e.signals??!1,data:e.data||{},encapsulation:e.encapsulation||bn.Emulated,styles:e.styles||pe,_:null,schemas:e.schemas||null,tView:null,id:""};n.standalone&&Ht("NgStandalone"),Xv(t);const o=e.dependencies;return t.directiveDefs=Ka(o,!1),t.pipeDefs=Ka(o,!0),t.id=function AN(e){let n=0;const o=[e.selectors,e.ngContentSelectors,e.hostVars,e.hostAttrs,"function"==typeof e.consts?"":e.consts,e.vars,e.decls,e.encapsulation,e.standalone,e.signals,e.exportAs,JSON.stringify(e.inputs),JSON.stringify(e.outputs),Object.getOwnPropertyNames(e.type.prototype),!!e.contentQueries,!!e.viewQuery];for(const r of o.join("|"))n=Math.imul(31,n)+r.charCodeAt(0)|0;return n+=2147483648,"c"+n}(t),t})}function MN(e){return ie(e)||ot(e)}function TN(e){return null!==e}function Gn(e){return yn(()=>({type:e.type,bootstrap:e.bootstrap||pe,declarations:e.declarations||pe,imports:e.imports||pe,exports:e.exports||pe,transitiveCompileScopes:null,schemas:e.schemas||null,id:e.id||null}))}function SN(e,n){if(null==e)return Xt;const t={};for(const o in e)if(e.hasOwnProperty(o)){const i=e[o];let r,s,a,l;Array.isArray(i)?(a=i[0],r=i[1],s=i[2]??r,l=i[3]||null):(r=i,s=i,a=xa.None,l=null),t[r]=[o,a,l],n[r]=s}return t}function ON(e){if(null==e)return Xt;const n={};for(const t in e)e.hasOwnProperty(t)&&(n[e[t]]=t);return n}function G(e){return yn(()=>{const n=Kv(e);return Xv(n),n})}function vt(e){return{type:e.type,name:e.name,factory:null,pure:!1!==e.pure,standalone:e.standalone??!0,onDestroy:e.type.prototype.ngOnDestroy||null}}function Kv(e){const n={};return{type:e.type,providersResolver:null,factory:null,hostBindings:e.hostBindings||null,hostVars:e.hostVars||0,hostAttrs:e.hostAttrs||null,contentQueries:e.contentQueries||null,declaredInputs:n,inputConfig:e.inputs||Xt,exportAs:e.exportAs||null,standalone:e.standalone??!0,signals:!0===e.signals,selectors:e.selectors||pe,viewQuery:e.viewQuery||null,features:e.features||null,setInput:null,resolveHostDirectives:null,hostDirectives:null,inputs:SN(e.inputs,n),outputs:ON(e.outputs),debugInfo:null}}function Xv(e){e.features?.forEach(n=>n(e))}function Ka(e,n){if(!e)return null;const t=n?tn:MN;return()=>("function"==typeof e?e():e).map(o=>t(o)).filter(TN)}function oe(e){let n=function ey(e){return Object.getPrototypeOf(e.prototype).constructor}(e.type),t=!0;const o=[e];for(;n;){let i;if(Tt(e))i=n.\u0275cmp||n.\u0275dir;else{if(n.\u0275cmp)throw new O(903,!1);i=n.\u0275dir}if(i){if(t){o.push(i);const s=e;s.inputs=of(e.inputs),s.declaredInputs=of(e.declaredInputs),s.outputs=of(e.outputs);const a=i.hostBindings;a&&LN(e,a);const l=i.viewQuery,c=i.contentQueries;if(l&&RN(e,l),c&&FN(e,c),xN(e,i),A1(e.outputs,i.outputs),Tt(i)&&i.data.animation){const u=e.data;u.animation=(u.animation||[]).concat(i.data.animation)}}const r=i.features;if(r)for(let s=0;s<r.length;s++){const a=r[s];a&&a.ngInherit&&a(e),a===oe&&(t=!1)}}n=Object.getPrototypeOf(n)}!function kN(e){let n=0,t=null;for(let o=e.length-1;o>=0;o--){const i=e[o];i.hostVars=n+=i.hostVars,i.hostAttrs=pi(i.hostAttrs,t=pi(t,i.hostAttrs))}}(o)}function xN(e,n){for(const t in n.inputs){if(!n.inputs.hasOwnProperty(t)||e.inputs.hasOwnProperty(t))continue;const o=n.inputs[t];void 0!==o&&(e.inputs[t]=o,e.declaredInputs[t]=n.declaredInputs[t])}}function of(e){return e===Xt?{}:e===pe?[]:e}function RN(e,n){const t=e.viewQuery;e.viewQuery=t?(o,i)=>{n(o,i),t(o,i)}:n}function FN(e,n){const t=e.contentQueries;e.contentQueries=t?(o,i,r)=>{n(o,i,r),t(o,i,r)}:n}function LN(e,n){const t=e.hostBindings;e.hostBindings=t?(o,i)=>{n(o,i),t(o,i)}:n}function ko(e,n,t,o,i,r,s,a,l,c,u){const d=t+q,g=n.firstCreatePass?function $N(e,n,t,o,i,r,s,a,l){const c=n.consts,u=Ti(n,e,4,s||null,a||null);ou()&&Ud(n,t,u,pt(c,l),gd),u.mergedAttrs=pi(u.mergedAttrs,u.attrs),vu(n,u);const d=u.tView=ld(2,u,o,i,r,n.directiveRegistry,n.pipeRegistry,null,n.schemas,c,null);return null!==n.queries&&(n.queries.template(n,u),d.queries=n.queries.embeddedTView(u)),u}(d,n,e,o,i,r,s,a,c):n.data[d];l&&(g.flags|=l),zt(g,!1);const h=ry(n,e,g,t);hr()&&Fa(n,e,h,g),_t(h,e);const p=W_(h,e,h,g);return e[d]=p,ud(e,p),ta(g)&&ka(n,e,g),null!=c&&dd(e,g,u),g}function Ro(e,n,t,o,i,r,s,a){const l=w(),c=J();return ko(l,c,e,n,t,o,i,pt(c.consts,r),void 0,s,a),Ro}let ry=function sy(e,n,t,o){return gr(!0),n[11].createComment("")};var sf=function(e){return e[e.CHANGE_DETECTION=0]="CHANGE_DETECTION",e[e.AFTER_NEXT_RENDER=1]="AFTER_NEXT_RENDER",e}(sf||{});const Qr=new R(""),dy=!1,me=class JN extends Kt{__isAsync;destroyRef=void 0;pendingTasks=void 0;constructor(n=!1){super(),this.__isAsync=n,Wc()&&(this.destroyRef=V(yo,{optional:!0})??void 0,this.pendingTasks=V(ci,{optional:!0})??void 0)}emit(n){const t=$(null);try{super.next(n)}finally{$(t)}}subscribe(n,t,o){let i=n,r=t||(()=>null),s=o;if(n&&"object"==typeof n){const l=n;i=l.next?.bind(l),r=l.error?.bind(l),s=l.complete?.bind(l)}this.__isAsync&&(r=this.wrapInTimeout(r),i&&(i=this.wrapInTimeout(i)),s&&(s=this.wrapInTimeout(s)));const a=super.subscribe({next:i,error:r,complete:s});return n instanceof Et&&n.add(a),a}wrapInTimeout(n){return t=>{const o=this.pendingTasks?.add();setTimeout(()=>{try{n(t)}finally{void 0!==o&&this.pendingTasks?.remove(o)}})}}};function fy(e){let n,t;function o(){e=pr;try{void 0!==t&&"function"==typeof cancelAnimationFrame&&cancelAnimationFrame(t),void 0!==n&&clearTimeout(n)}catch{}}return n=setTimeout(()=>{e(),o()}),"function"==typeof requestAnimationFrame&&(t=requestAnimationFrame(()=>{e(),o()})),()=>o()}function hy(e){return queueMicrotask(()=>e()),()=>{e=pr}}const af="isAngularZone",nl=af+"_ID";let ZN=0;class ae{hasPendingMacrotasks=!1;hasPendingMicrotasks=!1;isStable=!0;onUnstable=new me(!1);onMicrotaskEmpty=new me(!1);onStable=new me(!1);onError=new me(!1);constructor(n){const{enableLongStackTrace:t=!1,shouldCoalesceEventChangeDetection:o=!1,shouldCoalesceRunChangeDetection:i=!1,scheduleInRootZone:r=dy}=n;if(typeof Zone>"u")throw new O(908,!1);Zone.assertZonePatched();const s=this;s._nesting=0,s._outer=s._inner=Zone.current,Zone.TaskTrackingZoneSpec&&(s._inner=s._inner.fork(new Zone.TaskTrackingZoneSpec)),t&&Zone.longStackTraceZoneSpec&&(s._inner=s._inner.fork(Zone.longStackTraceZoneSpec)),s.shouldCoalesceEventChangeDetection=!i&&o,s.shouldCoalesceRunChangeDetection=i,s.callbackScheduled=!1,s.scheduleInRootZone=r,function XN(e){const n=()=>{!function KN(e){function n(){fy(()=>{e.callbackScheduled=!1,cf(e),e.isCheckStableRunning=!0,lf(e),e.isCheckStableRunning=!1})}e.isCheckStableRunning||e.callbackScheduled||(e.callbackScheduled=!0,e.scheduleInRootZone?Zone.root.run(()=>{n()}):e._outer.run(()=>{n()}),cf(e))}(e)},t=ZN++;e._inner=e._inner.fork({name:"angular",properties:{[af]:!0,[nl]:t,[nl+t]:!0},onInvokeTask:(o,i,r,s,a,l)=>{if(function eA(e){return my(e,"__ignore_ng_zone__")}(l))return o.invokeTask(r,s,a,l);try{return gy(e),o.invokeTask(r,s,a,l)}finally{(e.shouldCoalesceEventChangeDetection&&"eventTask"===s.type||e.shouldCoalesceRunChangeDetection)&&n(),py(e)}},onInvoke:(o,i,r,s,a,l,c)=>{try{return gy(e),o.invoke(r,s,a,l,c)}finally{e.shouldCoalesceRunChangeDetection&&!e.callbackScheduled&&!function tA(e){return my(e,"__scheduler_tick__")}(l)&&n(),py(e)}},onHasTask:(o,i,r,s)=>{o.hasTask(r,s),i===r&&("microTask"==s.change?(e._hasPendingMicrotasks=s.microTask,cf(e),lf(e)):"macroTask"==s.change&&(e.hasPendingMacrotasks=s.macroTask))},onHandleError:(o,i,r,s)=>(o.handleError(r,s),e.runOutsideAngular(()=>e.onError.emit(s)),!1)})}(s)}static isInAngularZone(){return typeof Zone<"u"&&!0===Zone.current.get(af)}static assertInAngularZone(){if(!ae.isInAngularZone())throw new O(909,!1)}static assertNotInAngularZone(){if(ae.isInAngularZone())throw new O(909,!1)}run(n,t,o){return this._inner.run(n,t,o)}runTask(n,t,o,i){const r=this._inner,s=r.scheduleEventTask("NgZoneEvent: "+i,n,QN,pr,pr);try{return r.runTask(s,t,o)}finally{r.cancelTask(s)}}runGuarded(n,t,o){return this._inner.runGuarded(n,t,o)}runOutsideAngular(n){return this._outer.run(n)}}const QN={};function lf(e){if(0==e._nesting&&!e.hasPendingMicrotasks&&!e.isStable)try{e._nesting++,e.onMicrotaskEmpty.emit(null)}finally{if(e._nesting--,!e.hasPendingMicrotasks)try{e.runOutsideAngular(()=>e.onStable.emit(null))}finally{e.isStable=!0}}}function cf(e){e.hasPendingMicrotasks=!!(e._hasPendingMicrotasks||(e.shouldCoalesceEventChangeDetection||e.shouldCoalesceRunChangeDetection)&&!0===e.callbackScheduled)}function gy(e){e._nesting++,e.isStable&&(e.isStable=!1,e.onUnstable.emit(null))}function py(e){e._nesting--,lf(e)}class uf{hasPendingMicrotasks=!1;hasPendingMacrotasks=!1;isStable=!0;onUnstable=new me;onMicrotaskEmpty=new me;onStable=new me;onError=new me;run(n,t,o){return n.apply(t,o)}runGuarded(n,t,o){return n.apply(t,o)}runOutsideAngular(n){return n()}runTask(n,t,o,i){return n.apply(t,o)}}function my(e,n){return!(!Array.isArray(e)||1!==e.length)&&!0===e[0]?.data?.[n]}let _y=(()=>{class e{impl=null;execute(){this.impl?.execute()}static \u0275prov=X({token:e,providedIn:"root",factory:()=>new e})}return e})();const Uy=new R(""),sl=new R("");let Cf,vf=(()=>{class e{_ngZone;registry;_isZoneStable=!0;_callbacks=[];_taskTrackingZone=null;_destroyRef;constructor(t,o,i){this._ngZone=t,this.registry=o,Wc()&&(this._destroyRef=V(yo,{optional:!0})??void 0),Cf||(function cx(e){Cf=e}(i),i.addToWindow(o)),this._watchAngularEvents(),t.run(()=>{this._taskTrackingZone=typeof Zone>"u"?null:Zone.current.get("TaskTrackingZone")})}_watchAngularEvents(){const t=this._ngZone.onUnstable.subscribe({next:()=>{this._isZoneStable=!1}}),o=this._ngZone.runOutsideAngular(()=>this._ngZone.onStable.subscribe({next:()=>{ae.assertNotInAngularZone(),queueMicrotask(()=>{this._isZoneStable=!0,this._runCallbacksIfReady()})}}));this._destroyRef?.onDestroy(()=>{t.unsubscribe(),o.unsubscribe()})}isStable(){return this._isZoneStable&&!this._ngZone.hasPendingMacrotasks}_runCallbacksIfReady(){if(this.isStable())queueMicrotask(()=>{for(;0!==this._callbacks.length;){let t=this._callbacks.pop();clearTimeout(t.timeoutId),t.doneCb()}});else{let t=this.getPendingTasks();this._callbacks=this._callbacks.filter(o=>!o.updateCb||!o.updateCb(t)||(clearTimeout(o.timeoutId),!1))}}getPendingTasks(){return this._taskTrackingZone?this._taskTrackingZone.macroTasks.map(t=>({source:t.source,creationLocation:t.creationLocation,data:t.data})):[]}addCallback(t,o,i){let r=-1;o&&o>0&&(r=setTimeout(()=>{this._callbacks=this._callbacks.filter(s=>s.timeoutId!==r),t()},o)),this._callbacks.push({doneCb:t,timeoutId:r,updateCb:i})}whenStable(t,o,i){if(i&&!this._taskTrackingZone)throw new Error('Task tracking zone is required when passing an update callback to whenStable(). Is "zone.js/plugins/task-tracking" loaded?');this.addCallback(t,o,i),this._runCallbacksIfReady()}registerApplication(t){this.registry.registerApplication(t,this)}unregisterApplication(t){this.registry.unregisterApplication(t)}findProviders(t,o,i){return[]}static \u0275fac=function(o){return new(o||e)(te(ae),te(yf),te(sl))};static \u0275prov=X({token:e,factory:e.\u0275fac})}return e})(),yf=(()=>{class e{_applications=new Map;registerApplication(t,o){this._applications.set(t,o)}unregisterApplication(t){this._applications.delete(t)}unregisterAllApplications(){this._applications.clear()}getTestability(t){return this._applications.get(t)||null}getAllTestabilities(){return Array.from(this._applications.values())}getAllRootElements(){return Array.from(this._applications.keys())}findTestabilityInTree(t,o=!0){return Cf?.findTestabilityInTree(this,t,o)??null}static \u0275fac=function(o){return new(o||e)};static \u0275prov=X({token:e,factory:e.\u0275fac,providedIn:"platform"})}return e})();function al(e){return!!e&&"function"==typeof e.then}function $y(e){return!!e&&"function"==typeof e.subscribe}const zy=new R("");let Gy=(()=>{class e{resolve;reject;initialized=!1;done=!1;donePromise=new Promise((t,o)=>{this.resolve=t,this.reject=o});appInits=V(zy,{optional:!0})??[];injector=V(Lt);constructor(){}runInitializers(){if(this.initialized)return;const t=[];for(const i of this.appInits){const r=Gg(this.injector,i);if(al(r))t.push(r);else if($y(r)){const s=new Promise((a,l)=>{r.subscribe({complete:a,error:l})});t.push(s)}}const o=()=>{this.done=!0,this.resolve()};Promise.all(t).then(()=>{o()}).catch(i=>{this.reject(i)}),0===t.length&&o(),this.initialized=!0}static \u0275fac=function(o){return new(o||e)};static \u0275prov=X({token:e,factory:e.\u0275fac,providedIn:"root"})}return e})();const ux=new R("");function qy(e,n){return Array.isArray(n)?n.reduce(qy,e):{...e,...n}}let Po=(()=>{class e{_runningTick=!1;_destroyed=!1;_destroyListeners=[];_views=[];internalErrorHandler=V(vn);afterRenderManager=V(_y);zonelessEnabled=V(Dp);rootEffectScheduler=V(Mp);dirtyFlags=0;tracingSnapshot=null;allTestViews=new Set;autoDetectTestViews=new Set;includeAllTestViews=!1;afterTick=new Kt;get allViews(){return[...(this.includeAllTestViews?this.allTestViews:this.autoDetectTestViews).keys(),...this._views]}get destroyed(){return this._destroyed}componentTypes=[];components=[];internalPendingTask=V(ci);get isStable(){return this.internalPendingTask.hasPendingTasksObservable.pipe(hu(t=>!t))}constructor(){V(Qr,{optional:!0})}whenStable(){let t;return new Promise(o=>{t=this.isStable.subscribe({next:i=>{i&&o()}})}).finally(()=>{t.unsubscribe()})}_injector=V(Rt);_rendererFactory=null;get injector(){return this._injector}bootstrap(t,o){return this.bootstrapImpl(t,o)}bootstrapImpl(t,o,i=Lt.NULL){return this._injector.get(ae).run(()=>{ce(10);const s=t instanceof Cv;if(!this._injector.get(Gy).done)throw new O(405,"");let l;l=s?t:this._injector.get(qa).resolveComponentFactory(t),this.componentTypes.push(l.componentType);const c=function fx(e){return e.isBoundToModule}(l)?void 0:this._injector.get(xo),d=l.create(i,[],o||l.selector,c),g=d.location.nativeElement,h=d.injector.get(Uy,null);return h?.registerApplication(g),d.onDestroy(()=>{this.detachView(d.hostView),ll(this.components,d),h?.unregisterApplication(g)}),this._loadComponent(d),ce(11,d),d})}tick(){this.zonelessEnabled||(this.dirtyFlags|=1),this._tick()}_tick(){ce(12),null!==this.tracingSnapshot?this.tracingSnapshot.run(sf.CHANGE_DETECTION,this.tickImpl):this.tickImpl()}tickImpl=()=>{if(this._runningTick)throw new O(101,!1);const t=$(null);try{this._runningTick=!0,this.synchronize()}finally{this._runningTick=!1,this.tracingSnapshot?.dispose(),this.tracingSnapshot=null,$(t),this.afterTick.next(),ce(13)}};synchronize(){null===this._rendererFactory&&!this._injector.destroyed&&(this._rendererFactory=this._injector.get(Vd,null,{optional:!0}));let t=0;for(;0!==this.dirtyFlags&&t++<10;)ce(14),this.synchronizeOnce(),ce(15)}synchronizeOnce(){16&this.dirtyFlags&&(this.dirtyFlags&=-17,this.rootEffectScheduler.flush());let t=!1;if(7&this.dirtyFlags){const o=!!(1&this.dirtyFlags);this.dirtyFlags&=-8,this.dirtyFlags|=8;for(let{_lView:i}of this.allViews)(o||oa(i))&&(Pa(i,o&&!this.zonelessEnabled?0:1),t=!0);if(this.dirtyFlags&=-5,this.syncDirtyFlagsWithViews(),23&this.dirtyFlags)return}t||(this._rendererFactory?.begin?.(),this._rendererFactory?.end?.()),8&this.dirtyFlags&&(this.dirtyFlags&=-9,this.afterRenderManager.execute()),this.syncDirtyFlagsWithViews()}syncDirtyFlagsWithViews(){this.allViews.some(({_lView:t})=>oa(t))?this.dirtyFlags|=2:this.dirtyFlags&=-8}attachView(t){const o=t;this._views.push(o),o.attachToAppRef(this)}detachView(t){const o=t;ll(this._views,o),o.detachFromAppRef()}_loadComponent(t){this.attachView(t.hostView);try{this.tick()}catch(i){this.internalErrorHandler(i)}this.components.push(t),this._injector.get(ux,[]).forEach(i=>i(t))}ngOnDestroy(){if(!this._destroyed)try{this._destroyListeners.forEach(t=>t()),this._views.slice().forEach(t=>t.destroy())}finally{this._destroyed=!0,this._views=[],this._destroyListeners=[]}}onDestroy(t){return this._destroyListeners.push(t),()=>ll(this._destroyListeners,t)}destroy(){if(this._destroyed)throw new O(406,!1);const t=this._injector;t.destroy&&!t.destroyed&&t.destroy()}get viewCount(){return this._views.length}static \u0275fac=function(o){return new(o||e)};static \u0275prov=X({token:e,factory:e.\u0275fac,providedIn:"root"})}return e})();function ll(e,n){const t=e.indexOf(n);t>-1&&e.splice(t,1)}function lt(e,n,t,o){const i=w();return De(i,St(),n)&&(J(),function QT(e,n,t,o,i,r){const s=gt(e,n);!function pd(e,n,t,o,i,r,s){if(null==r)e.removeAttribute(n,i,t);else{const a=null==s?W(r):s(r,o||"",i);e.setAttribute(n,i,a,t)}}(n[11],s,r,e.value,t,o,i)}(nn(),i,e,n,t,o)),lt}class Jx{destroy(n){}updateValue(n,t){}swap(n,t){const o=Math.min(n,t),i=Math.max(n,t),r=this.detach(i);if(i-o>1){const s=this.detach(o);this.attach(o,r),this.attach(i,s)}else this.attach(o,r)}move(n,t){this.attach(t,this.detach(n))}}function If(e,n,t,o,i){return e===t&&Object.is(n,o)?1:Object.is(i(e,n),i(t,o))?-1:0}function Mf(e,n,t,o){return!(void 0===n||!n.has(o)||(e.attach(t,n.get(o)),n.delete(o),0))}function tC(e,n,t,o,i){if(Mf(e,n,o,t(o,i)))e.updateValue(o,i);else{const r=e.create(o,i);e.attach(o,r)}}function nC(e,n,t,o){const i=new Set;for(let r=n;r<=t;r++)i.add(o(r,e.at(r)));return i}class oC{kvMap=new Map;_vMap=void 0;has(n){return this.kvMap.has(n)}delete(n){if(!this.has(n))return!1;const t=this.kvMap.get(n);return void 0!==this._vMap&&this._vMap.has(t)?(this.kvMap.set(n,this._vMap.get(t)),this._vMap.delete(t)):this.kvMap.delete(n),!0}get(n){return this.kvMap.get(n)}set(n,t){if(this.kvMap.has(n)){let o=this.kvMap.get(n);void 0===this._vMap&&(this._vMap=new Map);const i=this._vMap;for(;i.has(o);)o=i.get(o);i.set(o,t)}else this.kvMap.set(n,t)}forEach(n){for(let[t,o]of this.kvMap)if(n(o,t),void 0!==this._vMap){const i=this._vMap;for(;i.has(o);)o=i.get(o),n(o,t)}}}function y(e,n,t,o,i,r,s,a){Ht("NgControlFlow");const l=w(),c=J();return ko(l,c,e,n,t,o,i,pt(c.consts,r),256,s,a),Tf}function Tf(e,n,t,o,i,r,s,a){Ht("NgControlFlow");const l=w(),c=J();return ko(l,c,e,n,t,o,i,pt(c.consts,r),512,s,a),Tf}function C(e,n){Ht("NgControlFlow");const t=w(),o=St(),i=t[o]!==se?t[o]:-1,r=-1!==i?cl(t,q+i):void 0;if(De(t,o,e)){const a=$(null);try{if(void 0!==r&&Td(r,0),-1!==e){const l=q+e,c=cl(t,l),u=Sf(t[1],l),d=null;Mi(c,Ei(t,u,n,{dehydratedView:d}),0,To(u,d))}}finally{$(a)}}else if(void 0!==r){const a=Y_(r,0);void 0!==a&&(a[8]=n)}}class Qx{lContainer;$implicit;$index;constructor(n,t,o){this.lContainer=n,this.$implicit=t,this.$index=o}get $count(){return this.lContainer.length-10}}function Qe(e,n){return n}class Xx{hasEmptyBlock;trackByFn;liveCollection;constructor(n,t,o){this.hasEmptyBlock=n,this.trackByFn=t,this.liveCollection=o}}function Ke(e,n,t,o,i,r,s,a,l,c,u,d,g){Ht("NgControlFlow");const h=w(),p=J(),b=void 0!==l,M=w(),N=a?s.bind(M[15][8]):s,E=new Xx(b,N);M[q+e]=E,ko(h,p,e+1,n,t,o,i,pt(p.consts,r),256),b&&ko(h,p,e+2,l,c,u,d,pt(p.consts,g),512)}class ek extends Jx{lContainer;hostLView;templateTNode;operationsCounter=void 0;needsIndexUpdate=!1;constructor(n,t,o){super(),this.lContainer=n,this.hostLView=t,this.templateTNode=o}get length(){return this.lContainer.length-10}at(n){return this.getLView(n)[8].$implicit}attach(n,t){const o=t[6];this.needsIndexUpdate||=n!==this.length,Mi(this.lContainer,t,n,To(this.templateTNode,o))}detach(n){return this.needsIndexUpdate||=n!==this.length-1,function tk(e,n){return Lr(e,n)}(this.lContainer,n)}create(n,t){const i=Ei(this.hostLView,this.templateTNode,new Qx(this.lContainer,t,n),{dehydratedView:null});return this.operationsCounter?.recordCreate(),i}destroy(n){kr(n[1],n),this.operationsCounter?.recordDestroy()}updateValue(n,t){this.getLView(n)[8].$implicit=t}reset(){this.needsIndexUpdate=!1,this.operationsCounter?.reset()}updateIndexes(){if(this.needsIndexUpdate)for(let n=0;n<this.length;n++)this.getLView(n)[8].$index=n}getLView(n){return function nk(e,n){return Y_(e,n)}(this.lContainer,n)}}function Xe(e){const n=$(null),t=qe();try{const o=w(),i=o[1],r=o[t],s=t+1,a=cl(o,s);if(void 0===r.liveCollection){const c=Sf(i,s);r.liveCollection=new ek(a,o,c)}else r.liveCollection.reset();const l=r.liveCollection;if(function Zx(e,n,t){let o,i,r=0,s=e.length-1;if(Array.isArray(n)){let l=n.length-1;for(;r<=s&&r<=l;){const c=e.at(r),u=n[r],d=If(r,c,r,u,t);if(0!==d){d<0&&e.updateValue(r,u),r++;continue}const g=e.at(s),h=n[l],p=If(s,g,l,h,t);if(0!==p){p<0&&e.updateValue(s,h),s--,l--;continue}const b=t(r,c),M=t(s,g),N=t(r,u);if(Object.is(N,M)){const E=t(l,h);Object.is(E,b)?(e.swap(r,s),e.updateValue(s,h),l--,s--):e.move(s,r),e.updateValue(r,u),r++}else if(o??=new oC,i??=nC(e,r,s,t),Mf(e,o,r,N))e.updateValue(r,u),r++,s++;else if(i.has(N))o.set(b,e.detach(r)),s--;else{const E=e.create(r,n[r]);e.attach(r,E),r++,s++}}for(;r<=l;)tC(e,o,t,r,n[r]),r++}else if(null!=n){const l=n[Symbol.iterator]();let c=l.next();for(;!c.done&&r<=s;){const u=e.at(r),d=c.value,g=If(r,u,r,d,t);if(0!==g)g<0&&e.updateValue(r,d),r++,c=l.next();else{o??=new oC,i??=nC(e,r,s,t);const h=t(r,d);if(Mf(e,o,r,h))e.updateValue(r,d),r++,s++,c=l.next();else if(i.has(h)){const p=t(r,u);o.set(p,e.detach(r)),s--}else e.attach(r,e.create(r,d)),r++,s++,c=l.next()}}for(;!c.done;)tC(e,o,t,e.length,c.value),c=l.next()}for(;r<=s;)e.destroy(e.detach(s--));o?.forEach(l=>{e.destroy(l)})}(l,e,r.trackByFn),l.updateIndexes(),r.hasEmptyBlock){const c=St(),u=0===l.length;if(De(o,c,u)){const d=t+2,g=cl(o,d);if(u){const h=Sf(i,d),p=null;Mi(g,Ei(o,h,void 0,{dehydratedView:p}),0,To(h,p))}else i.firstUpdatePass&&function $a(e){const n=e[6]??[],o=e[3][11],i=[];for(const r of n)void 0!==r.data.di?i.push(r):gv(r,o);e[6]=i}(g),Td(g,0)}}}finally{$(n)}}function cl(e,n){return e[n]}function Sf(e,n){return ii(e,n)}function S(e,n,t){const o=w();return De(o,St(),n)&&(J(),fd(nn(),o,e,n,o[11],t)),S}function Of(e,n,t,o,i){_d(n,e,t,i?"class":"style",o)}function v(e,n,t,o){const i=w(),r=J(),s=q+e,a=i[11],l=r.firstCreatePass?Mv(s,r,i,n,gd,ou(),t,o):r.data[s],c=iC(r,i,l,a,n,e);i[s]=c;const u=ta(l);return zt(l,!0),D_(a,c,l),!So(l)&&hr()&&Fa(r,i,c,l),(0===function yI(){return z.lFrame.elementDepthCount}()||u)&&_t(c,i),function CI(){z.lFrame.elementDepthCount++}(),u&&(ka(r,i,l),Qu(r,l,i)),null!==o&&dd(i,l),v}function _(){let e=K();iu()?ru():(e=e.parent,zt(e,!1));const n=e;(function DI(e){return z.skipHydrationRootTNode===e})(n)&&function II(){z.skipHydrationRootTNode=null}(),function bI(){z.lFrame.elementDepthCount--}();const t=J();return t.firstCreatePass&&Tv(t,n),null!=n.classesWithoutHost&&function hM(e){return!!(8&e.flags)}(n)&&Of(t,n,w(),n.classesWithoutHost,!0),null!=n.stylesWithoutHost&&function gM(e){return!!(16&e.flags)}(n)&&Of(t,n,w(),n.stylesWithoutHost,!1),_}function x(e,n,t,o){return v(e,n,t,o),_(),x}let iC=(e,n,t,o,i,r)=>(gr(!0),Na(o,i,function RI(){return z.lFrame.currentNamespace}()));function ts(e,n,t){const o=w(),i=J(),r=e+q,s=i.firstCreatePass?function ik(e,n,t,o,i){const r=n.consts,s=pt(r,o),a=Ti(n,e,8,"ng-container",s);null!==s&&jd(a,s,!0);const l=pt(r,i);return ou()&&Ud(n,t,a,l,gd),a.mergedAttrs=pi(a.mergedAttrs,a.attrs),null!==n.queries&&n.queries.elementStart(n,a),a}(r,i,o,n,t):i.data[r];zt(s,!0);const a=sC(i,o,s,e);return o[r]=a,hr()&&Fa(i,o,a,s),_t(a,o),ta(s)&&(ka(i,o,s),Qu(i,s,o)),null!=t&&dd(o,s),ts}function ns(){let e=K();const n=J();return iu()?ru():(e=e.parent,zt(e,!1)),n.firstCreatePass&&(vu(n,e),Zc(e)&&n.queries.elementEnd(e)),ns}let sC=(e,n,t,o)=>(gr(!0),ad(n[11],""));function le(){return w()}const ul="en-US";let dC=ul;function j(e,n,t){const o=w(),i=J(),r=K();return xf(i,o,o[11],r,e,n,t),j}function xf(e,n,t,o,i,r,s){let a=!0,l=null;if((3&o.type||s)&&(l??=Za(o,n,r),function PO(e,n,t,o,i,r,s,a){const l=ta(e);let c=!1,u=null;if(!o&&l&&(u=function VO(e,n,t,o){const i=e.cleanup;if(null!=i)for(let r=0;r<i.length-1;r+=2){const s=i[r];if(s===t&&i[r+1]===o){const a=n[7],l=i[r+2];return a&&a.length>l?a[l]:null}"string"==typeof s&&(r+=2)}return null}(n,t,r,e.index)),null!==u)(u.__ngLastListenerFn__||u).__ngNextListenerFn__=s,u.__ngLastListenerFn__=s,c=!0;else{const d=gt(e,t),g=o?o(d):d,h=i.listen(g,r,a);Ov(o?b=>o($e(b[e.index])):e.index,n,t,r,a,h,!1)}return c}(o,e,n,s,t,i,r,l)&&(a=!1)),a){const c=o.outputs?.[i],u=o.hostDirectiveOutputs?.[i];if(u&&u.length)for(let d=0;d<u.length;d+=2){const g=u[d],h=u[d+1];l??=Za(o,n,r),Qa(o,n,g,h,i,l)}if(c&&c.length)for(const d of c)l??=Za(o,n,r),Qa(o,n,d,i,i,l)}}function m(e=1){return function NI(e){return(z.lFrame.contextLView=function ep(e,n){for(;e>0;)n=n[14],e--;return n}(e,z.lFrame.contextLView))[8]}(e)}function NC(e,n,t,o){!function Uv(e,n,t,o){const i=J();if(i.firstCreatePass){const r=K();$v(i,new Vv(n,t,o),r.index),function dN(e,n){const t=e.contentQueries||(e.contentQueries=[]);n!==(t.length?t[t.length-1]:-1)&&t.push(e.queries.length-1,n)}(i,e),!(2&~t)&&(i.staticContentQueries=!0)}return Bv(i,w(),t)}(e,n,t,o)}function Ot(e,n,t){!function jv(e,n,t){const o=J();return o.firstCreatePass&&($v(o,new Vv(e,n,t),-1),!(2&~n)&&(o.staticViewQueries=!0)),Bv(o,w(),n)}(e,n,t)}function Dt(e){const n=w(),t=J(),o=lu();sa(o+1);const i=Xd(t,o);if(e.dirty&&function mI(e){return!(4&~e[2])}(n)===!(2&~i.metadata.flags)){if(null===i.matches)e.reset([]);else{const r=zv(n,o);e.reset(r,tm),e.notifyOnChanges()}return!0}return!1}function wt(){return function Kd(e,n){return e[18].queries[n].queryList}(w(),lu())}function gl(e,n){return e<<17|n<<2}function jo(e){return e>>17&32767}function kf(e){return 2|e}function Hi(e){return(131068&e)>>2}function Rf(e,n){return-131069&e|n<<2}function Ff(e){return 1|e}function AC(e,n,t,o){const i=e[t+1],r=null===n;let s=o?jo(i):Hi(i),a=!1;for(;0!==s&&(!1===a||r);){const c=e[s+1];lR(e[s],n)&&(a=!0,e[s+1]=o?Ff(c):kf(c)),s=o?jo(c):Hi(c)}a&&(e[t+1]=o?kf(i):Ff(i))}function lR(e,n){return null===e||null==n||(Array.isArray(e)?e[1]:e)===n||!(!Array.isArray(e)||"string"!=typeof n)&&ar(e,n)>=0}const Ve={textEnd:0,key:0,keyEnd:0,value:0,valueEnd:0};function xC(e){return e.substring(Ve.key,Ve.keyEnd)}function kC(e,n){const t=Ve.textEnd;return t===n?-1:(n=Ve.keyEnd=function fR(e,n,t){for(;n<t&&e.charCodeAt(n)>32;)n++;return n}(e,Ve.key=n,t),Bi(e,n,t))}function Bi(e,n,t){for(;n<t&&e.charCodeAt(n)<=32;)n++;return n}function pl(e,n,t){return VC(e,n,t,!1),pl}function En(e,n){return VC(e,n,null,!0),En}function Bt(e){!function HC(e,n,t,o){const i=J(),r=_n(2);i.firstUpdatePass&&jC(i,null,r,o);const s=w();if(t!==se&&De(s,r,t)){const a=i.data[qe()];if(GC(a,o)&&!BC(i,r)){let l=o?a.classesWithoutHost:a.stylesWithoutHost;null!==l&&(t=Tc(l,t||"")),Of(i,a,s,t,o)}else!function ER(e,n,t,o,i,r,s,a){i===se&&(i=pe);let l=0,c=0,u=0<i.length?i[0]:null,d=0<r.length?r[0]:null;for(;null!==u||null!==d;){const g=l<i.length?i[l+1]:void 0,h=c<r.length?r[c+1]:void 0;let b,p=null;u===d?(l+=2,c+=2,g!==h&&(p=d,b=h)):null===d||null!==u&&u<d?(l+=2,p=u):(c+=2,p=d,b=h),null!==p&&$C(e,n,t,o,p,b,s,a),u=l<i.length?i[l]:null,d=c<r.length?r[c]:null}}(i,a,s,s[11],s[r+1],s[r+1]=function DR(e,n,t){if(null==t||""===t)return pe;const o=[],i=Bn(t);if(Array.isArray(i))for(let r=0;r<i.length;r++)e(o,i[r],!0);else if("object"==typeof i)for(const r in i)i.hasOwnProperty(r)&&e(o,r,i[r]);else"string"==typeof i&&n(o,i);return o}(e,n,t),o,r)}}(wR,_R,e,!0)}function _R(e,n){for(let t=function uR(e){return function FC(e){Ve.key=0,Ve.keyEnd=0,Ve.value=0,Ve.valueEnd=0,Ve.textEnd=e.length}(e),kC(e,Bi(e,0,Ve.textEnd))}(n);t>=0;t=kC(n,t))Js(e,xC(n),!0)}function VC(e,n,t,o){const i=w(),r=J(),s=_n(2);r.firstUpdatePass&&jC(r,e,s,o),n!==se&&De(i,s,n)&&$C(r,r.data[qe()],i,i[11],e,i[s+1]=function IR(e,n){return null==e||""===e||("string"==typeof n?e+=n:"object"==typeof e&&(e=Ge(Bn(e)))),e}(n,t),o,s)}function BC(e,n){return n>=e.expandoStartIndex}function jC(e,n,t,o){const i=e.data;if(null===i[t+1]){const r=i[qe()],s=BC(e,t);GC(r,o)&&null===n&&!s&&(n=!1),n=function vR(e,n,t,o){const i=function au(e){const n=z.lFrame.currentDirectiveIndex;return-1===n?null:e[n]}(e);let r=o?n.residualClasses:n.residualStyles;if(null===i)0===(o?n.classBindings:n.styleBindings)&&(t=ls(t=Lf(null,e,n,t,o),n.attrs,o),r=null);else{const s=n.directiveStylingLast;if(-1===s||e[s]!==i)if(t=Lf(i,e,n,t,o),null===r){let l=function yR(e,n,t){const o=t?n.classBindings:n.styleBindings;if(0!==Hi(o))return e[jo(o)]}(e,n,o);void 0!==l&&Array.isArray(l)&&(l=Lf(null,e,n,l[1],o),l=ls(l,n.attrs,o),function CR(e,n,t,o){e[jo(t?n.classBindings:n.styleBindings)]=o}(e,n,o,l))}else r=function bR(e,n,t){let o;const i=n.directiveEnd;for(let r=1+n.directiveStylingLast;r<i;r++)o=ls(o,e[r].hostAttrs,t);return ls(o,n.attrs,t)}(e,n,o)}return void 0!==r&&(o?n.residualClasses=r:n.residualStyles=r),t}(i,r,n,o),function sR(e,n,t,o,i,r){let s=r?n.classBindings:n.styleBindings,a=jo(s),l=Hi(s);e[o]=t;let u,c=!1;if(Array.isArray(t)?(u=t[1],(null===u||ar(t,u)>0)&&(c=!0)):u=t,i)if(0!==l){const g=jo(e[a+1]);e[o+1]=gl(g,a),0!==g&&(e[g+1]=Rf(e[g+1],o)),e[a+1]=function iR(e,n){return 131071&e|n<<17}(e[a+1],o)}else e[o+1]=gl(a,0),0!==a&&(e[a+1]=Rf(e[a+1],o)),a=o;else e[o+1]=gl(l,0),0===a?a=o:e[l+1]=Rf(e[l+1],o),l=o;c&&(e[o+1]=kf(e[o+1])),AC(e,u,o,!0),AC(e,u,o,!1),function aR(e,n,t,o,i){const r=i?e.residualClasses:e.residualStyles;null!=r&&"string"==typeof n&&ar(r,n)>=0&&(t[o+1]=Ff(t[o+1]))}(n,u,e,o,r),s=gl(a,l),r?n.classBindings=s:n.styleBindings=s}(i,r,n,t,s,o)}}function Lf(e,n,t,o,i){let r=null;const s=t.directiveEnd;let a=t.directiveStylingLast;for(-1===a?a=t.directiveStart:a++;a<s&&(r=n[a],o=ls(o,r.hostAttrs,i),r!==e);)a++;return null!==e&&(t.directiveStylingLast=a),o}function ls(e,n,t){const o=t?1:2;let i=-1;if(null!==n)for(let r=0;r<n.length;r++){const s=n[r];"number"==typeof s?i=s:i===o&&(Array.isArray(e)||(e=void 0===e?[]:["",e]),Js(e,s,!!t||n[++r]))}return void 0===e?null:e}function wR(e,n,t){const o=String(n);""!==o&&!o.includes(" ")&&Js(e,o,t)}function $C(e,n,t,o,i,r,s,a){if(!(3&n.type))return;const l=e.data,c=l[a+1],u=function rR(e){return!(1&~e)}(c)?zC(l,n,t,i,Hi(c),s):void 0;ml(u)||(ml(r)||function oR(e){return!(2&~e)}(c)&&(r=zC(l,null,t,i,a,s)),function dS(e,n,t,o,i){if(n)i?e.addClass(t,o):e.removeClass(t,o);else{let r=-1===o.indexOf("-")?void 0:Un.DashCase;null==i?e.removeStyle(t,o,r):("string"==typeof i&&i.endsWith("!important")&&(i=i.slice(0,-10),r|=Un.Important),e.setStyle(t,o,i,r))}}(o,s,oi(qe(),t),i,r))}function zC(e,n,t,o,i,r){const s=null===n;let a;for(;i>0;){const l=e[i],c=Array.isArray(l),u=c?l[1]:l,d=null===u;let g=t[i+1];g===se&&(g=d?pe:void 0);let h=d?Lc(g,o):u===o?g:void 0;if(c&&!ml(h)&&(h=Lc(l,o)),ml(h)&&(a=h,s))return a;const p=e[i+1];i=s?jo(p):Hi(p)}if(null!==n){let l=r?n.residualClasses:n.residualStyles;null!=l&&(a=Lc(l,o))}return a}function ml(e){return void 0!==e}function GC(e,n){return!!(e.flags&(n?8:16))}function D(e,n=""){const t=w(),o=J(),i=e+q,r=o.firstCreatePass?Ti(o,i,1,n,null):o.data[i],s=qC(o,t,r,n,e);t[i]=s,hr()&&Fa(o,t,s,r),zt(r,!1)}let qC=(e,n,t,o,i)=>(gr(!0),function sd(e,n){return e.createText(n)}(n[11],o));function YC(e,n,t,o=""){return De(e,St(),t)?n+W(t)+o:se}function k(e){return L("",e),k}function L(e,n,t){const o=w(),i=YC(o,e,n,t);return i!==se&&function In(e,n,t){const o=oi(n,e);!function v_(e,n,t){e.setValue(n,t)}(e[11],o,t)}(o,qe(),i),L}function He(e,n,t){bp(n)&&(n=n());const o=w();return De(o,St(),n)&&(J(),fd(nn(),o,e,n,o[11],t)),He}function _e(e,n){const t=bp(e);return t&&e.set(n),t}function ze(e,n){const t=w(),o=J(),i=K();return xf(o,t,t[11],i,e,n),ze}function Mn(e){return De(w(),St(),e)?W(e):se}function jt(e,n,t=""){return YC(w(),e,n,t)}function Pf(e,n,t,o,i){if(e=Y(e),Array.isArray(e))for(let r=0;r<e.length;r++)Pf(e[r],n,t,o,i);else{const r=J(),s=w(),a=K();let l=An(e)?e:Y(e.provide);const c=zg(e),u=1048575&a.providerIndexes,d=a.directiveStart,g=a.providerIndexes>>20;if(An(e)||!e.multi){const h=new yr(c,i,T),p=Hf(l,n,i?u:u+g,d);-1===p?(Eu(da(a,s),r,l),Vf(r,e,n.length),n.push(l),a.directiveStart++,a.directiveEnd++,i&&(a.providerIndexes+=1048576),t.push(h),s.push(h)):(t[p]=h,s[p]=h)}else{const h=Hf(l,n,u+g,d),p=Hf(l,n,u,u+g),M=p>=0&&t[p];if(i&&!M||!i&&!(h>=0&&t[h])){Eu(da(a,s),r,l);const N=function jR(e,n,t,o,i){const r=new yr(e,t,T);return r.multi=[],r.index=n,r.componentProviders=0,fb(r,i,o&&!t),r}(i?BR:HR,t.length,i,o,c);!i&&M&&(t[p].providerFactory=N),Vf(r,e,n.length,0),n.push(l),a.directiveStart++,a.directiveEnd++,i&&(a.providerIndexes+=1048576),t.push(N),s.push(N)}else Vf(r,e,h>-1?h:p,fb(t[i?p:h],c,!i&&o));!i&&o&&M&&t[p].componentProviders++}}}function Vf(e,n,t,o){const i=An(n),r=function rI(e){return!!e.useClass}(n);if(i||r){const l=(r?Y(n.useClass):n).prototype.ngOnDestroy;if(l){const c=e.destroyHooks||(e.destroyHooks=[]);if(!i&&n.multi){const u=c.indexOf(t);-1===u?c.push(t,[o,l]):c[u+1].push(o,l)}else c.push(t,l)}}}function fb(e,n,t){return t&&e.componentProviders++,e.multi.push(n)-1}function Hf(e,n,t,o){for(let i=t;i<o;i++)if(n[i]===e)return i;return-1}function HR(e,n,t,o){return Bf(this.multi,[])}function BR(e,n,t,o){const i=this.multi;let r;if(this.providerFactory){const s=this.providerFactory.componentProviders,a=Dr(t,t[1],this.providerFactory.index,o);r=a.slice(0,s),Bf(i,r);for(let l=s;l<a.length;l++)r.push(a[l])}else r=[],Bf(i,r);return r}function Bf(e,n){for(let t=0;t<e.length;t++)n.push((0,e[t])());return n}function be(e,n=[]){return t=>{t.providersResolver=(o,i)=>function VR(e,n,t){const o=J();if(o.firstCreatePass){const i=Tt(e);Pf(t,o.data,o.blueprint,i,!0),Pf(n,o.data,o.blueprint,i,!1)}}(o,i?i(e):e,n)}}function ji(e,n,t,o){return function gb(e,n,t,o,i,r){const s=n+t;return De(e,s,i)?sn(e,s+1,r?o.call(r,i):o(i)):cs(e,s+1)}(w(),st(),e,n,t,o)}function jf(e,n,t,o,i){return function pb(e,n,t,o,i,r,s){const a=n+t;return Ao(e,a,i,r)?sn(e,a+2,s?o.call(s,i,r):o(i,r)):cs(e,a+2)}(w(),st(),e,n,t,o,i)}function Ne(e,n,t,o,i,r){return mb(w(),st(),e,n,t,o,i,r)}function cs(e,n){const t=e[n];return t===se?void 0:t}function mb(e,n,t,o,i,r,s,a){const l=n+t;return function Ja(e,n,t,o,i){const r=Ao(e,n,t,o);return De(e,n+2,i)||r}(e,l,i,r,s)?sn(e,l+3,a?o.call(a,i,r,s):o(i,r,s)):cs(e,l+3)}let kF=(()=>{class e{zone=V(ae);changeDetectionScheduler=V(li);applicationRef=V(Po);applicationErrorHandler=V(vn);_onMicrotaskEmptySubscription;initialize(){this._onMicrotaskEmptySubscription||(this._onMicrotaskEmptySubscription=this.zone.onMicrotaskEmpty.subscribe({next:()=>{this.changeDetectionScheduler.runningTick||this.zone.run(()=>{try{this.applicationRef.dirtyFlags|=1,this.applicationRef._tick()}catch(t){this.applicationErrorHandler(t)}})}}))}ngOnDestroy(){this._onMicrotaskEmptySubscription?.unsubscribe()}static \u0275fac=function(o){return new(o||e)};static \u0275prov=X({token:e,factory:e.\u0275fac,providedIn:"root"})}return e})();function Vb({ngZoneFactory:e,ignoreChangesOutsideZone:n,scheduleInRootZone:t}){return e??=()=>new ae({...qf(),scheduleInRootZone:t}),[{provide:ae,useFactory:e},{provide:uo,multi:!0,useFactory:()=>{const o=V(kF,{optional:!0});return()=>o.initialize()}},{provide:uo,multi:!0,useFactory:()=>{const o=V(FF);return()=>{o.initialize()}}},!0===n?{provide:wp,useValue:!0}:[],{provide:Ep,useValue:t??dy},{provide:vn,useFactory:()=>{const o=V(ae),i=V(Rt);let r;return s=>{r??=i.get(ai),o.runOutsideAngular(()=>r.handleError(s))}}}]}function qf(e){return{enableLongStackTrace:!1,shouldCoalesceEventChangeDetection:e?.eventCoalescing??!1,shouldCoalesceRunChangeDetection:e?.runCoalescing??!1}}let FF=(()=>{class e{subscription=new Et;initialized=!1;zone=V(ae);pendingTasks=V(ci);initialize(){if(this.initialized)return;this.initialized=!0;let t=null;!this.zone.isStable&&!this.zone.hasPendingMacrotasks&&!this.zone.hasPendingMicrotasks&&(t=this.pendingTasks.add()),this.zone.runOutsideAngular(()=>{this.subscription.add(this.zone.onStable.subscribe(()=>{ae.assertNotInAngularZone(),queueMicrotask(()=>{null!==t&&!this.zone.hasPendingMacrotasks&&!this.zone.hasPendingMicrotasks&&(this.pendingTasks.remove(t),t=null)})}))}),this.subscription.add(this.zone.onUnstable.subscribe(()=>{ae.assertInAngularZone(),t??=this.pendingTasks.add()}))}ngOnDestroy(){this.subscription.unsubscribe()}static \u0275fac=function(o){return new(o||e)};static \u0275prov=X({token:e,factory:e.\u0275fac,providedIn:"root"})}return e})(),jb=(()=>{class e{applicationErrorHandler=V(vn);appRef=V(Po);taskService=V(ci);ngZone=V(ae);zonelessEnabled=V(Dp);tracing=V(Qr,{optional:!0});disableScheduling=V(wp,{optional:!0})??!1;zoneIsDefined=typeof Zone<"u"&&!!Zone.root.run;schedulerTickApplyArgs=[{data:{__scheduler_tick__:!0}}];subscriptions=new Et;angularZoneId=this.zoneIsDefined?this.ngZone._inner?.get(nl):null;scheduleInRootZone=!this.zonelessEnabled&&this.zoneIsDefined&&(V(Ep,{optional:!0})??!1);cancelScheduledCallback=null;useMicrotaskScheduler=!1;runningTick=!1;pendingRenderTaskId=null;constructor(){this.subscriptions.add(this.appRef.afterTick.subscribe(()=>{this.runningTick||this.cleanup()})),this.subscriptions.add(this.ngZone.onUnstable.subscribe(()=>{this.runningTick||this.cleanup()})),this.disableScheduling||=!this.zonelessEnabled&&(this.ngZone instanceof uf||!this.zoneIsDefined)}notify(t){if(!this.zonelessEnabled&&5===t)return;let o=!1;switch(t){case 0:this.appRef.dirtyFlags|=2;break;case 3:case 2:case 4:case 5:case 1:this.appRef.dirtyFlags|=4;break;case 6:case 13:this.appRef.dirtyFlags|=2,o=!0;break;case 12:this.appRef.dirtyFlags|=16,o=!0;break;case 11:o=!0;break;default:this.appRef.dirtyFlags|=8}if(this.appRef.tracingSnapshot=this.tracing?.snapshot(this.appRef.tracingSnapshot)??null,!this.shouldScheduleTick(o))return;const i=this.useMicrotaskScheduler?hy:fy;this.pendingRenderTaskId=this.taskService.add(),this.cancelScheduledCallback=this.scheduleInRootZone?Zone.root.run(()=>i(()=>this.tick())):this.ngZone.runOutsideAngular(()=>i(()=>this.tick()))}shouldScheduleTick(t){return!(this.disableScheduling&&!t||this.appRef.destroyed||null!==this.pendingRenderTaskId||this.runningTick||this.appRef._runningTick||!this.zonelessEnabled&&this.zoneIsDefined&&Zone.current.get(nl+this.angularZoneId))}tick(){if(this.runningTick||this.appRef.destroyed)return;if(0===this.appRef.dirtyFlags)return void this.cleanup();!this.zonelessEnabled&&7&this.appRef.dirtyFlags&&(this.appRef.dirtyFlags|=1);const t=this.taskService.add();try{this.ngZone.run(()=>{this.runningTick=!0,this.appRef._tick()},void 0,this.schedulerTickApplyArgs)}catch(o){this.taskService.remove(t),this.applicationErrorHandler(o)}finally{this.cleanup()}this.useMicrotaskScheduler=!0,hy(()=>{this.useMicrotaskScheduler=!1,this.taskService.remove(t)})}ngOnDestroy(){this.subscriptions.unsubscribe(),this.cleanup()}cleanup(){if(this.runningTick=!1,this.cancelScheduledCallback?.(),this.cancelScheduledCallback=null,null!==this.pendingRenderTaskId){const t=this.pendingRenderTaskId;this.pendingRenderTaskId=null,this.taskService.remove(t)}}static \u0275fac=function(o){return new(o||e)};static \u0275prov=X({token:e,factory:e.\u0275fac,providedIn:"root"})}return e})();const Jn=new R("",{providedIn:"root",factory:()=>V(Jn,{optional:!0,skipSelf:!0})||function LF(){return typeof $localize<"u"&&$localize.locale||ul}()});new R("").__NG_ELEMENT_ID__=e=>{const n=K();if(null===n)throw new O(204,!1);if(2&n.type)return n.value;if(8&e)return null;throw new O(204,!1)};const wl=new R(""),JF=new R("");function fs(e){return!e.moduleRef}let Kb;function Xb(){Kb=ZF}function ZF(e,n){const t=e.injector.get(Po);if(e._bootstrapComponents.length>0)e._bootstrapComponents.forEach(o=>t.bootstrap(o));else{if(!e.instance.ngDoBootstrap)throw new O(-403,!1);e.instance.ngDoBootstrap(t)}n.push(e)}let eD=(()=>{class e{_injector;_modules=[];_destroyListeners=[];_destroyed=!1;constructor(t){this._injector=t}bootstrapModuleFactory(t,o){const i=o?.scheduleInRootZone,s=o?.ignoreChangesOutsideZone,a=[Vb({ngZoneFactory:()=>function nA(e="zone.js",n){return"noop"===e?new uf:"zone.js"===e?new ae(n):e}(o?.ngZone,{...qf({eventCoalescing:o?.ngZoneEventCoalescing,runCoalescing:o?.ngZoneRunCoalescing}),scheduleInRootZone:i}),ignoreChangesOutsideZone:s}),{provide:li,useExisting:jb},LI],l=function wN(e,n,t){return new nf(e,n,t,!1)}(t.moduleType,this.injector,a);return Xb(),function Qb(e){const n=fs(e)?e.r3Injector:e.moduleRef.injector,t=n.get(ae);return t.run(()=>{fs(e)?e.r3Injector.resolveInjectorInitializers():e.moduleRef.resolveInjectorInitializers();const o=n.get(vn);let i;if(t.runOutsideAngular(()=>{i=t.onError.subscribe({next:o})}),fs(e)){const r=()=>n.destroy(),s=e.platformInjector.get(wl);s.add(r),n.onDestroy(()=>{i.unsubscribe(),s.delete(r)})}else{const r=()=>e.moduleRef.destroy(),s=e.platformInjector.get(wl);s.add(r),e.moduleRef.onDestroy(()=>{ll(e.allPlatformModules,e.moduleRef),i.unsubscribe(),s.delete(r)})}return function QF(e,n,t){try{const o=t();return al(o)?o.catch(i=>{throw n.runOutsideAngular(()=>e(i)),i}):o}catch(o){throw n.runOutsideAngular(()=>e(o)),o}}(o,t,()=>{const r=n.get(Gy);return r.runInitializers(),r.donePromise.then(()=>{if(function fk(e){"string"==typeof e&&(dC=e.toLowerCase().replace(/_/g,"-"))}(n.get(Jn,ul)||ul),!n.get(JF,!0))return fs(e)?n.get(Po):(e.allPlatformModules.push(e.moduleRef),e.moduleRef);if(fs(e)){const l=n.get(Po);return void 0!==e.rootComponent&&l.bootstrap(e.rootComponent),l}return Kb?.(e.moduleRef,e.allPlatformModules),e.moduleRef})})})}({moduleRef:l,allPlatformModules:this._modules,platformInjector:this.injector})}bootstrapModule(t,o=[]){const i=qy({},o);return Xb(),function GF(e,n,t){const o=new Zv(t);return Promise.resolve(o)}(0,0,t).then(r=>this.bootstrapModuleFactory(r,i))}onDestroy(t){this._destroyListeners.push(t)}get injector(){return this._injector}destroy(){if(this._destroyed)throw new O(404,!1);this._modules.slice().forEach(o=>o.destroy()),this._destroyListeners.forEach(o=>o());const t=this._injector.get(wl,null);t&&(t.forEach(o=>o()),t.clear()),this._destroyed=!0}get destroyed(){return this._destroyed}static \u0275fac=function(o){return new(o||e)(te(Lt))};static \u0275prov=X({token:e,factory:e.\u0275fac,providedIn:"platform"})}return e})(),Zn=null;const tD=new R("");function nD(e,n,t=[]){const o=`Platform: ${n}`,i=new R(o);return(r=[])=>{let s=Kf();if(!s||s.injector.get(tD,!1)){const a=[...t,...r,{provide:i,useValue:!0}];e?e(a):function KF(e){if(Zn&&!Zn.get(tD,!1))throw new O(400,!1);(function dx(){!function g1(e){hg=e}(()=>{throw new O(600,"")})})(),Zn=e;const n=e.get(eD);(function iD(e){const n=e.get(bm,null);Gg(e,()=>{n?.forEach(t=>t())})})(e)}(function oD(e=[],n){return Lt.create({name:n,providers:[{provide:Uc,useValue:"platform"},{provide:wl,useValue:new Set([()=>Zn=null])},...e]})}(a,o))}return function XF(){const n=Kf();if(!n)throw new O(401,!1);return n}()}}function Kf(){return Zn?.get(eD)??null}let zi=(()=>class e{static __NG_ELEMENT_ID__=tL})();function tL(e){return function nL(e,n,t){if(Rn(e)&&!t){const o=rt(e.index,n);return new Pr(o,o)}return 175&e.type?new Pr(n[15],n):null}(K(),w(),!(16&~e))}class sD{constructor(){}supports(n){return Ya(n)}create(n){return new iL(n)}}const oL=(e,n)=>n;class iL{length=0;collection;_linkedRecords=null;_unlinkedRecords=null;_previousItHead=null;_itHead=null;_itTail=null;_additionsHead=null;_additionsTail=null;_movesHead=null;_movesTail=null;_removalsHead=null;_removalsTail=null;_identityChangesHead=null;_identityChangesTail=null;_trackByFn;constructor(n){this._trackByFn=n||oL}forEachItem(n){let t;for(t=this._itHead;null!==t;t=t._next)n(t)}forEachOperation(n){let t=this._itHead,o=this._removalsHead,i=0,r=null;for(;t||o;){const s=!o||t&&t.currentIndex<lD(o,i,r)?t:o,a=lD(s,i,r),l=s.currentIndex;if(s===o)i--,o=o._nextRemoved;else if(t=t._next,null==s.previousIndex)i++;else{r||(r=[]);const c=a-i,u=l-i;if(c!=u){for(let g=0;g<c;g++){const h=g<r.length?r[g]:r[g]=0,p=h+g;u<=p&&p<c&&(r[g]=h+1)}r[s.previousIndex]=u-c}}a!==l&&n(s,a,l)}}forEachPreviousItem(n){let t;for(t=this._previousItHead;null!==t;t=t._nextPrevious)n(t)}forEachAddedItem(n){let t;for(t=this._additionsHead;null!==t;t=t._nextAdded)n(t)}forEachMovedItem(n){let t;for(t=this._movesHead;null!==t;t=t._nextMoved)n(t)}forEachRemovedItem(n){let t;for(t=this._removalsHead;null!==t;t=t._nextRemoved)n(t)}forEachIdentityChange(n){let t;for(t=this._identityChangesHead;null!==t;t=t._nextIdentityChange)n(t)}diff(n){if(null==n&&(n=[]),!Ya(n))throw new O(900,!1);return this.check(n)?this:null}onDestroy(){}check(n){this._reset();let i,r,s,t=this._itHead,o=!1;if(Array.isArray(n)){this.length=n.length;for(let a=0;a<this.length;a++)r=n[a],s=this._trackByFn(a,r),null!==t&&Object.is(t.trackById,s)?(o&&(t=this._verifyReinsertion(t,r,s,a)),Object.is(t.item,r)||this._addIdentityChange(t,r)):(t=this._mismatch(t,r,s,a),o=!0),t=t._next}else i=0,function FO(e,n){if(Array.isArray(e))for(let t=0;t<e.length;t++)n(e[t]);else{const t=e[Symbol.iterator]();let o;for(;!(o=t.next()).done;)n(o.value)}}(n,a=>{s=this._trackByFn(i,a),null!==t&&Object.is(t.trackById,s)?(o&&(t=this._verifyReinsertion(t,a,s,i)),Object.is(t.item,a)||this._addIdentityChange(t,a)):(t=this._mismatch(t,a,s,i),o=!0),t=t._next,i++}),this.length=i;return this._truncate(t),this.collection=n,this.isDirty}get isDirty(){return null!==this._additionsHead||null!==this._movesHead||null!==this._removalsHead||null!==this._identityChangesHead}_reset(){if(this.isDirty){let n;for(n=this._previousItHead=this._itHead;null!==n;n=n._next)n._nextPrevious=n._next;for(n=this._additionsHead;null!==n;n=n._nextAdded)n.previousIndex=n.currentIndex;for(this._additionsHead=this._additionsTail=null,n=this._movesHead;null!==n;n=n._nextMoved)n.previousIndex=n.currentIndex;this._movesHead=this._movesTail=null,this._removalsHead=this._removalsTail=null,this._identityChangesHead=this._identityChangesTail=null}}_mismatch(n,t,o,i){let r;return null===n?r=this._itTail:(r=n._prev,this._remove(n)),null!==(n=null===this._unlinkedRecords?null:this._unlinkedRecords.get(o,null))?(Object.is(n.item,t)||this._addIdentityChange(n,t),this._reinsertAfter(n,r,i)):null!==(n=null===this._linkedRecords?null:this._linkedRecords.get(o,i))?(Object.is(n.item,t)||this._addIdentityChange(n,t),this._moveAfter(n,r,i)):n=this._addAfter(new rL(t,o),r,i),n}_verifyReinsertion(n,t,o,i){let r=null===this._unlinkedRecords?null:this._unlinkedRecords.get(o,null);return null!==r?n=this._reinsertAfter(r,n._prev,i):n.currentIndex!=i&&(n.currentIndex=i,this._addToMoves(n,i)),n}_truncate(n){for(;null!==n;){const t=n._next;this._addToRemovals(this._unlink(n)),n=t}null!==this._unlinkedRecords&&this._unlinkedRecords.clear(),null!==this._additionsTail&&(this._additionsTail._nextAdded=null),null!==this._movesTail&&(this._movesTail._nextMoved=null),null!==this._itTail&&(this._itTail._next=null),null!==this._removalsTail&&(this._removalsTail._nextRemoved=null),null!==this._identityChangesTail&&(this._identityChangesTail._nextIdentityChange=null)}_reinsertAfter(n,t,o){null!==this._unlinkedRecords&&this._unlinkedRecords.remove(n);const i=n._prevRemoved,r=n._nextRemoved;return null===i?this._removalsHead=r:i._nextRemoved=r,null===r?this._removalsTail=i:r._prevRemoved=i,this._insertAfter(n,t,o),this._addToMoves(n,o),n}_moveAfter(n,t,o){return this._unlink(n),this._insertAfter(n,t,o),this._addToMoves(n,o),n}_addAfter(n,t,o){return this._insertAfter(n,t,o),this._additionsTail=null===this._additionsTail?this._additionsHead=n:this._additionsTail._nextAdded=n,n}_insertAfter(n,t,o){const i=null===t?this._itHead:t._next;return n._next=i,n._prev=t,null===i?this._itTail=n:i._prev=n,null===t?this._itHead=n:t._next=n,null===this._linkedRecords&&(this._linkedRecords=new aD),this._linkedRecords.put(n),n.currentIndex=o,n}_remove(n){return this._addToRemovals(this._unlink(n))}_unlink(n){null!==this._linkedRecords&&this._linkedRecords.remove(n);const t=n._prev,o=n._next;return null===t?this._itHead=o:t._next=o,null===o?this._itTail=t:o._prev=t,n}_addToMoves(n,t){return n.previousIndex===t||(this._movesTail=null===this._movesTail?this._movesHead=n:this._movesTail._nextMoved=n),n}_addToRemovals(n){return null===this._unlinkedRecords&&(this._unlinkedRecords=new aD),this._unlinkedRecords.put(n),n.currentIndex=null,n._nextRemoved=null,null===this._removalsTail?(this._removalsTail=this._removalsHead=n,n._prevRemoved=null):(n._prevRemoved=this._removalsTail,this._removalsTail=this._removalsTail._nextRemoved=n),n}_addIdentityChange(n,t){return n.item=t,this._identityChangesTail=null===this._identityChangesTail?this._identityChangesHead=n:this._identityChangesTail._nextIdentityChange=n,n}}class rL{item;trackById;currentIndex=null;previousIndex=null;_nextPrevious=null;_prev=null;_next=null;_prevDup=null;_nextDup=null;_prevRemoved=null;_nextRemoved=null;_nextAdded=null;_nextMoved=null;_nextIdentityChange=null;constructor(n,t){this.item=n,this.trackById=t}}class sL{_head=null;_tail=null;add(n){null===this._head?(this._head=this._tail=n,n._nextDup=null,n._prevDup=null):(this._tail._nextDup=n,n._prevDup=this._tail,n._nextDup=null,this._tail=n)}get(n,t){let o;for(o=this._head;null!==o;o=o._nextDup)if((null===t||t<=o.currentIndex)&&Object.is(o.trackById,n))return o;return null}remove(n){const t=n._prevDup,o=n._nextDup;return null===t?this._head=o:t._nextDup=o,null===o?this._tail=t:o._prevDup=t,null===this._head}}class aD{map=new Map;put(n){const t=n.trackById;let o=this.map.get(t);o||(o=new sL,this.map.set(t,o)),o.add(n)}get(n,t){const i=this.map.get(n);return i?i.get(n,t):null}remove(n){const t=n.trackById;return this.map.get(t).remove(n)&&this.map.delete(t),n}get isEmpty(){return 0===this.map.size}clear(){this.map.clear()}}function lD(e,n,t){const o=e.previousIndex;if(null===o)return o;let i=0;return t&&o<t.length&&(i=t[o]),o+n+i}class cD{constructor(){}supports(n){return n instanceof Map||$d(n)}create(){return new aL}}class aL{_records=new Map;_mapHead=null;_appendAfter=null;_previousMapHead=null;_changesHead=null;_changesTail=null;_additionsHead=null;_additionsTail=null;_removalsHead=null;_removalsTail=null;get isDirty(){return null!==this._additionsHead||null!==this._changesHead||null!==this._removalsHead}forEachItem(n){let t;for(t=this._mapHead;null!==t;t=t._next)n(t)}forEachPreviousItem(n){let t;for(t=this._previousMapHead;null!==t;t=t._nextPrevious)n(t)}forEachChangedItem(n){let t;for(t=this._changesHead;null!==t;t=t._nextChanged)n(t)}forEachAddedItem(n){let t;for(t=this._additionsHead;null!==t;t=t._nextAdded)n(t)}forEachRemovedItem(n){let t;for(t=this._removalsHead;null!==t;t=t._nextRemoved)n(t)}diff(n){if(n){if(!(n instanceof Map||$d(n)))throw new O(900,!1)}else n=new Map;return this.check(n)?this:null}onDestroy(){}check(n){this._reset();let t=this._mapHead;if(this._appendAfter=null,this._forEach(n,(o,i)=>{if(t&&t.key===i)this._maybeAddToChanges(t,o),this._appendAfter=t,t=t._next;else{const r=this._getOrCreateRecordForKey(i,o);t=this._insertBeforeOrAppend(t,r)}}),t){t._prev&&(t._prev._next=null),this._removalsHead=t;for(let o=t;null!==o;o=o._nextRemoved)o===this._mapHead&&(this._mapHead=null),this._records.delete(o.key),o._nextRemoved=o._next,o.previousValue=o.currentValue,o.currentValue=null,o._prev=null,o._next=null}return this._changesTail&&(this._changesTail._nextChanged=null),this._additionsTail&&(this._additionsTail._nextAdded=null),this.isDirty}_insertBeforeOrAppend(n,t){if(n){const o=n._prev;return t._next=n,t._prev=o,n._prev=t,o&&(o._next=t),n===this._mapHead&&(this._mapHead=t),this._appendAfter=n,n}return this._appendAfter?(this._appendAfter._next=t,t._prev=this._appendAfter):this._mapHead=t,this._appendAfter=t,null}_getOrCreateRecordForKey(n,t){if(this._records.has(n)){const i=this._records.get(n);this._maybeAddToChanges(i,t);const r=i._prev,s=i._next;return r&&(r._next=s),s&&(s._prev=r),i._next=null,i._prev=null,i}const o=new lL(n);return this._records.set(n,o),o.currentValue=t,this._addToAdditions(o),o}_reset(){if(this.isDirty){let n;for(this._previousMapHead=this._mapHead,n=this._previousMapHead;null!==n;n=n._next)n._nextPrevious=n._next;for(n=this._changesHead;null!==n;n=n._nextChanged)n.previousValue=n.currentValue;for(n=this._additionsHead;null!=n;n=n._nextAdded)n.previousValue=n.currentValue;this._changesHead=this._changesTail=null,this._additionsHead=this._additionsTail=null,this._removalsHead=null}}_maybeAddToChanges(n,t){Object.is(t,n.currentValue)||(n.previousValue=n.currentValue,n.currentValue=t,this._addToChanges(n))}_addToAdditions(n){null===this._additionsHead?this._additionsHead=this._additionsTail=n:(this._additionsTail._nextAdded=n,this._additionsTail=n)}_addToChanges(n){null===this._changesHead?this._changesHead=this._changesTail=n:(this._changesTail._nextChanged=n,this._changesTail=n)}_forEach(n,t){n instanceof Map?n.forEach(t):Object.keys(n).forEach(o=>t(n[o],o))}}class lL{key;previousValue=null;currentValue=null;_nextPrevious=null;_next=null;_prev=null;_nextAdded=null;_nextRemoved=null;_nextChanged=null;constructor(n){this.key=n}}function uD(){return new Xf([new sD])}let Xf=(()=>{class e{factories;static \u0275prov=X({token:e,providedIn:"root",factory:uD});constructor(t){this.factories=t}static create(t,o){if(null!=o){const i=o.factories.slice();t=t.concat(i)}return new e(t)}static extend(t){return{provide:e,useFactory:o=>e.create(t,o||uD()),deps:[[e,new mu,new pu]]}}find(t){const o=this.factories.find(i=>i.supports(t));if(null!=o)return o;throw new O(901,!1)}}return e})();function dD(){return new El([new cD])}let El=(()=>{class e{static \u0275prov=X({token:e,providedIn:"root",factory:dD});factories;constructor(t){this.factories=t}static create(t,o){if(o){const i=o.factories.slice();t=t.concat(i)}return new e(t)}static extend(t){return{provide:e,useFactory:o=>e.create(t,o||dD()),deps:[[e,new mu,new pu]]}}find(t){const o=this.factories.find(i=>i.supports(t));if(o)return o;throw new O(901,!1)}}return e})();const dL=nD(null,"core",[]);let fL=(()=>{class e{constructor(t){}static \u0275fac=function(o){return new(o||e)(te(Po))};static \u0275mod=Gn({type:e});static \u0275inj=dn({})}return e})();function Be(e){return function _P(e){const n=$(null);try{return e()}finally{$(n)}}(e)}function ln(e,n){return function d1(e,n){const t=Object.create(f1);t.computation=e,void 0!==n&&(t.equal=n);const o=()=>{if(Xi(t),As(t),t.value===Sn)throw t.error;return t.value};return o[Ye]=t,o}(e,n?.equal)}Error,Error;const mh=/\s+/,KD=[];let qi=(()=>{class e{_ngEl;_renderer;initialClasses=KD;rawClass;stateMap=new Map;constructor(t,o){this._ngEl=t,this._renderer=o}set klass(t){this.initialClasses=null!=t?t.trim().split(mh):KD}set ngClass(t){this.rawClass="string"==typeof t?t.trim().split(mh):t}ngDoCheck(){for(const o of this.initialClasses)this._updateState(o,!0);const t=this.rawClass;if(Array.isArray(t)||t instanceof Set)for(const o of t)this._updateState(o,!0);else if(null!=t)for(const o of Object.keys(t))this._updateState(o,!!t[o]);this._applyStateDiff()}_updateState(t,o){const i=this.stateMap.get(t);void 0!==i?(i.enabled!==o&&(i.changed=!0,i.enabled=o),i.touched=!0):this.stateMap.set(t,{enabled:o,changed:!0,touched:!0})}_applyStateDiff(){for(const t of this.stateMap){const o=t[0],i=t[1];i.changed?(this._toggleClass(o,i.enabled),i.changed=!1):i.touched||(i.enabled&&this._toggleClass(o,!1),this.stateMap.delete(o)),i.touched=!1}}_toggleClass(t,o){(t=t.trim()).length>0&&t.split(mh).forEach(i=>{o?this._renderer.addClass(this._ngEl.nativeElement,i):this._renderer.removeClass(this._ngEl.nativeElement,i)})}static \u0275fac=function(o){return new(o||e)(T(at),T(qt))};static \u0275dir=G({type:e,selectors:[["","ngClass",""]],inputs:{klass:[0,"class","klass"],ngClass:"ngClass"}})}return e})();class m2{$implicit;ngForOf;index;count;constructor(n,t,o,i){this.$implicit=n,this.ngForOf=t,this.index=o,this.count=i}get first(){return 0===this.index}get last(){return this.index===this.count-1}get even(){return this.index%2==0}get odd(){return!this.even}}let ew=(()=>{class e{_viewContainer;_template;_differs;set ngForOf(t){this._ngForOf=t,this._ngForOfDirty=!0}set ngForTrackBy(t){this._trackByFn=t}get ngForTrackBy(){return this._trackByFn}_ngForOf=null;_ngForOfDirty=!0;_differ=null;_trackByFn;constructor(t,o,i){this._viewContainer=t,this._template=o,this._differs=i}set ngForTemplate(t){t&&(this._template=t)}ngDoCheck(){if(this._ngForOfDirty){this._ngForOfDirty=!1;const t=this._ngForOf;!this._differ&&t&&(this._differ=this._differs.find(t).create(this.ngForTrackBy))}if(this._differ){const t=this._differ.diff(this._ngForOf);t&&this._applyChanges(t)}}_applyChanges(t){const o=this._viewContainer;t.forEachOperation((i,r,s)=>{if(null==i.previousIndex)o.createEmbeddedView(this._template,new m2(i.item,this._ngForOf,-1,-1),null===s?void 0:s);else if(null==s)o.remove(null===r?void 0:r);else if(null!==r){const a=o.get(r);o.move(a,s),tw(a,i)}});for(let i=0,r=o.length;i<r;i++){const a=o.get(i).context;a.index=i,a.count=r,a.ngForOf=this._ngForOf}t.forEachIdentityChange(i=>{tw(o.get(i.currentIndex),i)})}static ngTemplateContextGuard(t,o){return!0}static \u0275fac=function(o){return new(o||e)(T(an),T(wn),T(Xf))};static \u0275dir=G({type:e,selectors:[["","ngFor","","ngForOf",""]],inputs:{ngForOf:"ngForOf",ngForTrackBy:"ngForTrackBy",ngForTemplate:"ngForTemplate"}})}return e})();function tw(e,n){e.context.$implicit=n.item}let _h=(()=>{class e{_viewContainer;_context=new _2;_thenTemplateRef=null;_elseTemplateRef=null;_thenViewRef=null;_elseViewRef=null;constructor(t,o){this._viewContainer=t,this._thenTemplateRef=o}set ngIf(t){this._context.$implicit=this._context.ngIf=t,this._updateView()}set ngIfThen(t){nw(t),this._thenTemplateRef=t,this._thenViewRef=null,this._updateView()}set ngIfElse(t){nw(t),this._elseTemplateRef=t,this._elseViewRef=null,this._updateView()}_updateView(){this._context.$implicit?this._thenViewRef||(this._viewContainer.clear(),this._elseViewRef=null,this._thenTemplateRef&&(this._thenViewRef=this._viewContainer.createEmbeddedView(this._thenTemplateRef,this._context))):this._elseViewRef||(this._viewContainer.clear(),this._thenViewRef=null,this._elseTemplateRef&&(this._elseViewRef=this._viewContainer.createEmbeddedView(this._elseTemplateRef,this._context)))}static ngIfUseIfTypeGuard;static ngTemplateGuard_ngIf;static ngTemplateContextGuard(t,o){return!0}static \u0275fac=function(o){return new(o||e)(T(an),T(wn))};static \u0275dir=G({type:e,selectors:[["","ngIf",""]],inputs:{ngIf:"ngIf",ngIfThen:"ngIfThen",ngIfElse:"ngIfElse"}})}return e})();class _2{$implicit=null;ngIf=null}function nw(e,n){if(e&&!e.createEmbeddedView)throw new O(2020,!1)}let iw=(()=>{class e{_ngEl;_differs;_renderer;_ngStyle=null;_differ=null;constructor(t,o,i){this._ngEl=t,this._differs=o,this._renderer=i}set ngStyle(t){this._ngStyle=t,!this._differ&&t&&(this._differ=this._differs.find(t).create())}ngDoCheck(){if(this._differ){const t=this._differ.diff(this._ngStyle);t&&this._applyChanges(t)}}_setStyle(t,o){const[i,r]=t.split("."),s=-1===i.indexOf("-")?void 0:Un.DashCase;null!=o?this._renderer.setStyle(this._ngEl.nativeElement,i,r?`${o}${r}`:o,s):this._renderer.removeStyle(this._ngEl.nativeElement,i,s)}_applyChanges(t){t.forEachRemovedItem(o=>this._setStyle(o.key,null)),t.forEachAddedItem(o=>this._setStyle(o.key,o.currentValue)),t.forEachChangedItem(o=>this._setStyle(o.key,o.currentValue))}static \u0275fac=function(o){return new(o||e)(T(at),T(El),T(qt))};static \u0275dir=G({type:e,selectors:[["","ngStyle",""]],inputs:{ngStyle:"ngStyle"}})}return e})(),rw=(()=>{class e{_viewContainerRef;_viewRef=null;ngTemplateOutletContext=null;ngTemplateOutlet=null;ngTemplateOutletInjector=null;constructor(t){this._viewContainerRef=t}ngOnChanges(t){if(this._shouldRecreateView(t)){const o=this._viewContainerRef;if(this._viewRef&&o.remove(o.indexOf(this._viewRef)),!this.ngTemplateOutlet)return void(this._viewRef=null);const i=this._createContextForwardProxy();this._viewRef=o.createEmbeddedView(this.ngTemplateOutlet,i,{injector:this.ngTemplateOutletInjector??void 0})}}_shouldRecreateView(t){return!!t.ngTemplateOutlet||!!t.ngTemplateOutletInjector}_createContextForwardProxy(){return new Proxy({},{set:(t,o,i)=>!!this.ngTemplateOutletContext&&Reflect.set(this.ngTemplateOutletContext,o,i),get:(t,o,i)=>{if(this.ngTemplateOutletContext)return Reflect.get(this.ngTemplateOutletContext,o,i)}})}static \u0275fac=function(o){return new(o||e)(T(an))};static \u0275dir=G({type:e,selectors:[["","ngTemplateOutlet",""]],inputs:{ngTemplateOutletContext:"ngTemplateOutletContext",ngTemplateOutlet:"ngTemplateOutlet",ngTemplateOutletInjector:"ngTemplateOutletInjector"},features:[Cn]})}return e})();let aw=(()=>{class e{transform(t,o,i){if(null==t)return null;if("string"!=typeof t&&!Array.isArray(t))throw function Jt(e,n){return new O(2100,!1)}();return t.slice(o,i)}static \u0275fac=function(o){return new(o||e)};static \u0275pipe=vt({name:"slice",type:e,pure:!1})}return e})(),lw=(()=>{class e{static \u0275fac=function(o){return new(o||e)};static \u0275mod=Gn({type:e});static \u0275inj=dn({})}return e})();const Dh=new R("");let cw=(()=>{class e{_zone;_plugins;_eventNameToPlugin=new Map;constructor(t,o){this._zone=o,t.forEach(i=>{i.manager=this}),this._plugins=t.slice().reverse()}addEventListener(t,o,i,r){return this._findPluginFor(o).addEventListener(t,o,i,r)}getZone(){return this._zone}_findPluginFor(t){let o=this._eventNameToPlugin.get(t);if(o)return o;if(o=this._plugins.find(r=>r.supports(t)),!o)throw new O(5101,!1);return this._eventNameToPlugin.set(t,o),o}static \u0275fac=function(o){return new(o||e)(te(Dh),te(ae))};static \u0275prov=X({token:e,factory:e.\u0275fac})}return e})();class uw{_doc;constructor(n){this._doc=n}manager}const $l="ng-app-id";function dw(e){for(const n of e)n.remove()}function fw(e,n){const t=n.createElement("style");return t.textContent=e,t}function wh(e,n){const t=n.createElement("link");return t.setAttribute("rel","stylesheet"),t.setAttribute("href",e),t}let hw=(()=>{class e{doc;appId;nonce;inline=new Map;external=new Map;hosts=new Set;isServer;constructor(t,o,i,r={}){this.doc=t,this.appId=o,this.nonce=i,this.isServer=function HL(e){return"server"===e}(r),function $2(e,n,t,o){const i=e.head?.querySelectorAll(`style[${$l}="${n}"],link[${$l}="${n}"]`);if(i)for(const r of i)r.removeAttribute($l),r instanceof HTMLLinkElement?o.set(r.href.slice(r.href.lastIndexOf("/")+1),{usage:0,elements:[r]}):r.textContent&&t.set(r.textContent,{usage:0,elements:[r]})}(t,o,this.inline,this.external),this.hosts.add(t.head)}addStyles(t,o){for(const i of t)this.addUsage(i,this.inline,fw);o?.forEach(i=>this.addUsage(i,this.external,wh))}removeStyles(t,o){for(const i of t)this.removeUsage(i,this.inline);o?.forEach(i=>this.removeUsage(i,this.external))}addUsage(t,o,i){const r=o.get(t);r?r.usage++:o.set(t,{usage:1,elements:[...this.hosts].map(s=>this.addElement(s,i(t,this.doc)))})}removeUsage(t,o){const i=o.get(t);i&&(i.usage--,i.usage<=0&&(dw(i.elements),o.delete(t)))}ngOnDestroy(){for(const[,{elements:t}]of[...this.inline,...this.external])dw(t);this.hosts.clear()}addHost(t){this.hosts.add(t);for(const[o,{elements:i}]of this.inline)i.push(this.addElement(t,fw(o,this.doc)));for(const[o,{elements:i}]of this.external)i.push(this.addElement(t,wh(o,this.doc)))}removeHost(t){this.hosts.delete(t)}addElement(t,o){return this.nonce&&o.setAttribute("nonce",this.nonce),this.isServer&&o.setAttribute($l,this.appId),t.appendChild(o)}static \u0275fac=function(o){return new(o||e)(te(Ln),te(va),te(Dm,8),te(ku))};static \u0275prov=X({token:e,factory:e.\u0275fac})}return e})();const Eh={svg:"http://www.w3.org/2000/svg",xhtml:"http://www.w3.org/1999/xhtml",xlink:"http://www.w3.org/1999/xlink",xml:"http://www.w3.org/XML/1998/namespace",xmlns:"http://www.w3.org/2000/xmlns/",math:"http://www.w3.org/1998/Math/MathML"},Ih=/%COMP%/g,J2=new R("",{providedIn:"root",factory:()=>!0});function pw(e,n){return n.map(t=>t.replace(Ih,e))}let mw=(()=>{class e{eventManager;sharedStylesHost;appId;removeStylesOnCompDestroy;doc;platformId;ngZone;nonce;tracingService;rendererByCompId=new Map;defaultRenderer;platformIsServer;constructor(t,o,i,r,s,a,l,c=null,u=null){this.eventManager=t,this.sharedStylesHost=o,this.appId=i,this.removeStylesOnCompDestroy=r,this.doc=s,this.platformId=a,this.ngZone=l,this.nonce=c,this.tracingService=u,this.platformIsServer=!1,this.defaultRenderer=new Mh(t,s,l,this.platformIsServer,this.tracingService)}createRenderer(t,o){if(!t||!o)return this.defaultRenderer;const i=this.getOrCreateRenderer(t,o);return i instanceof vw?i.applyToHost(t):i instanceof Th&&i.applyStyles(),i}getOrCreateRenderer(t,o){const i=this.rendererByCompId;let r=i.get(o.id);if(!r){const s=this.doc,a=this.ngZone,l=this.eventManager,c=this.sharedStylesHost,u=this.removeStylesOnCompDestroy,d=this.platformIsServer,g=this.tracingService;switch(o.encapsulation){case bn.Emulated:r=new vw(l,c,o,this.appId,u,s,a,d,g);break;case bn.ShadowDom:return new X2(l,c,t,o,s,a,this.nonce,d,g);default:r=new Th(l,c,o,u,s,a,d,g)}i.set(o.id,r)}return r}ngOnDestroy(){this.rendererByCompId.clear()}componentReplaced(t){this.rendererByCompId.delete(t)}static \u0275fac=function(o){return new(o||e)(te(cw),te(hw),te(va),te(J2),te(Ln),te(ku),te(ae),te(Dm),te(Qr,8))};static \u0275prov=X({token:e,factory:e.\u0275fac})}return e})();class Mh{eventManager;doc;ngZone;platformIsServer;tracingService;data=Object.create(null);throwOnSyntheticProps=!0;constructor(n,t,o,i,r){this.eventManager=n,this.doc=t,this.ngZone=o,this.platformIsServer=i,this.tracingService=r}destroy(){}destroyNode=null;createElement(n,t){return t?this.doc.createElementNS(Eh[t]||t,n):this.doc.createElement(n)}createComment(n){return this.doc.createComment(n)}createText(n){return this.doc.createTextNode(n)}appendChild(n,t){(_w(n)?n.content:n).appendChild(t)}insertBefore(n,t,o){n&&(_w(n)?n.content:n).insertBefore(t,o)}removeChild(n,t){t.remove()}selectRootElement(n,t){let o="string"==typeof n?this.doc.querySelector(n):n;if(!o)throw new O(-5104,!1);return t||(o.textContent=""),o}parentNode(n){return n.parentNode}nextSibling(n){return n.nextSibling}setAttribute(n,t,o,i){if(i){t=i+":"+t;const r=Eh[i];r?n.setAttributeNS(r,t,o):n.setAttribute(t,o)}else n.setAttribute(t,o)}removeAttribute(n,t,o){if(o){const i=Eh[o];i?n.removeAttributeNS(i,t):n.removeAttribute(`${o}:${t}`)}else n.removeAttribute(t)}addClass(n,t){n.classList.add(t)}removeClass(n,t){n.classList.remove(t)}setStyle(n,t,o,i){i&(Un.DashCase|Un.Important)?n.style.setProperty(t,o,i&Un.Important?"important":""):n.style[t]=o}removeStyle(n,t,o){o&Un.DashCase?n.style.removeProperty(t):n.style[t]=""}setProperty(n,t,o){null!=n&&(n[t]=o)}setValue(n,t){n.nodeValue=t}listen(n,t,o,i){if("string"==typeof n&&!(n=mr().getGlobalEventTarget(this.doc,n)))throw new O(5102,!1);let r=this.decoratePreventDefault(o);return this.tracingService?.wrapEventListener&&(r=this.tracingService.wrapEventListener(n,t,r)),this.eventManager.addEventListener(n,t,r,i)}decoratePreventDefault(n){return t=>{if("__ngUnwrap__"===t)return n;!1===n(t)&&t.preventDefault()}}}function _w(e){return"TEMPLATE"===e.tagName&&void 0!==e.content}class X2 extends Mh{sharedStylesHost;hostEl;shadowRoot;constructor(n,t,o,i,r,s,a,l,c){super(n,r,s,l,c),this.sharedStylesHost=t,this.hostEl=o,this.shadowRoot=o.attachShadow({mode:"open"}),this.sharedStylesHost.addHost(this.shadowRoot);let u=i.styles;u=pw(i.id,u);for(const g of u){const h=document.createElement("style");a&&h.setAttribute("nonce",a),h.textContent=g,this.shadowRoot.appendChild(h)}const d=i.getExternalStyles?.();if(d)for(const g of d){const h=wh(g,r);a&&h.setAttribute("nonce",a),this.shadowRoot.appendChild(h)}}nodeOrShadowRoot(n){return n===this.hostEl?this.shadowRoot:n}appendChild(n,t){return super.appendChild(this.nodeOrShadowRoot(n),t)}insertBefore(n,t,o){return super.insertBefore(this.nodeOrShadowRoot(n),t,o)}removeChild(n,t){return super.removeChild(null,t)}parentNode(n){return this.nodeOrShadowRoot(super.parentNode(this.nodeOrShadowRoot(n)))}destroy(){this.sharedStylesHost.removeHost(this.shadowRoot)}}class Th extends Mh{sharedStylesHost;removeStylesOnCompDestroy;styles;styleUrls;constructor(n,t,o,i,r,s,a,l,c){super(n,r,s,a,l),this.sharedStylesHost=t,this.removeStylesOnCompDestroy=i;let u=o.styles;this.styles=c?pw(c,u):u,this.styleUrls=o.getExternalStyles?.(c)}applyStyles(){this.sharedStylesHost.addStyles(this.styles,this.styleUrls)}destroy(){this.removeStylesOnCompDestroy&&this.sharedStylesHost.removeStyles(this.styles,this.styleUrls)}}class vw extends Th{contentAttr;hostAttr;constructor(n,t,o,i,r,s,a,l,c){const u=i+"-"+o.id;super(n,t,o,r,s,a,l,c,u),this.contentAttr=function Z2(e){return"_ngcontent-%COMP%".replace(Ih,e)}(u),this.hostAttr=function Q2(e){return"_nghost-%COMP%".replace(Ih,e)}(u)}applyToHost(n){this.applyStyles(),this.setAttribute(n,this.hostAttr,"")}createElement(n,t){const o=super.createElement(n,t);return super.setAttribute(o,this.contentAttr,""),o}}class Sh extends jI{supportsDOMEvents=!0;static makeCurrent(){!function BI(e){Tp??=e}(new Sh)}onAndCancel(n,t,o,i){return n.addEventListener(t,o,i),()=>{n.removeEventListener(t,o,i)}}dispatchEvent(n,t){n.dispatchEvent(t)}remove(n){n.remove()}createElement(n,t){return(t=t||this.getDefaultDocument()).createElement(n)}createHtmlDocument(){return document.implementation.createHTMLDocument("fakeTitle")}getDefaultDocument(){return document}isElementNode(n){return n.nodeType===Node.ELEMENT_NODE}isShadowRoot(n){return n instanceof DocumentFragment}getGlobalEventTarget(n,t){return"window"===t?window:"document"===t?n:"body"===t?n.body:null}getBaseHref(n){const t=function eV(){return vs=vs||document.head.querySelector("base"),vs?vs.getAttribute("href"):null}();return null==t?null:function tV(e){return new URL(e,document.baseURI).pathname}(t)}resetBaseElement(){vs=null}getUserAgent(){return window.navigator.userAgent}getCookie(n){return function zI(e,n){n=encodeURIComponent(n);for(const t of e.split(";")){const o=t.indexOf("="),[i,r]=-1==o?[t,""]:[t.slice(0,o),t.slice(o+1)];if(i.trim()===n)return decodeURIComponent(r)}return null}(document.cookie,n)}}let vs=null,oV=(()=>{class e{build(){return new XMLHttpRequest}static \u0275fac=function(o){return new(o||e)};static \u0275prov=X({token:e,factory:e.\u0275fac})}return e})(),iV=(()=>{class e extends uw{constructor(t){super(t)}supports(t){return!0}addEventListener(t,o,i,r){return t.addEventListener(o,i,r),()=>this.removeEventListener(t,o,i,r)}removeEventListener(t,o,i,r){return t.removeEventListener(o,i,r)}static \u0275fac=function(o){return new(o||e)(te(Ln))};static \u0275prov=X({token:e,factory:e.\u0275fac})}return e})();const yw=["alt","control","meta","shift"],rV={"\b":"Backspace","\t":"Tab","\x7f":"Delete","\x1b":"Escape",Del:"Delete",Esc:"Escape",Left:"ArrowLeft",Right:"ArrowRight",Up:"ArrowUp",Down:"ArrowDown",Menu:"ContextMenu",Scroll:"ScrollLock",Win:"OS"},sV={alt:e=>e.altKey,control:e=>e.ctrlKey,meta:e=>e.metaKey,shift:e=>e.shiftKey};let aV=(()=>{class e extends uw{constructor(t){super(t)}supports(t){return null!=e.parseEventName(t)}addEventListener(t,o,i,r){const s=e.parseEventName(o),a=e.eventCallback(s.fullKey,i,this.manager.getZone());return this.manager.getZone().runOutsideAngular(()=>mr().onAndCancel(t,s.domEventName,a,r))}static parseEventName(t){const o=t.toLowerCase().split("."),i=o.shift();if(0===o.length||"keydown"!==i&&"keyup"!==i)return null;const r=e._normalizeKey(o.pop());let s="",a=o.indexOf("code");if(a>-1&&(o.splice(a,1),s="code."),yw.forEach(c=>{const u=o.indexOf(c);u>-1&&(o.splice(u,1),s+=c+".")}),s+=r,0!=o.length||0===r.length)return null;const l={};return l.domEventName=i,l.fullKey=s,l}static matchEventFullKeyCode(t,o){let i=rV[t.key]||t.key,r="";return o.indexOf("code.")>-1&&(i=t.code,r="code."),!(null==i||!i)&&(i=i.toLowerCase()," "===i?i="space":"."===i&&(i="dot"),yw.forEach(s=>{s!==i&&(0,sV[s])(t)&&(r+=s+".")}),r+=i,r===o)}static eventCallback(t,o,i){return r=>{e.matchEventFullKeyCode(r,t)&&i.runGuarded(()=>o(r))}}static _normalizeKey(t){return"esc"===t?"escape":t}static \u0275fac=function(o){return new(o||e)(te(Ln))};static \u0275prov=X({token:e,factory:e.\u0275fac})}return e})();const dV=nD(dL,"browser",[{provide:ku,useValue:"browser"},{provide:bm,useValue:function lV(){Sh.makeCurrent()},multi:!0},{provide:Ln,useFactory:function uV(){return function s0(e){xu=e}(document),document}}]),Dw=[{provide:sl,useClass:class nV{addToWindow(n){Ee.getAngularTestability=(o,i=!0)=>{const r=n.findTestabilityInTree(o,i);if(null==r)throw new O(5103,!1);return r},Ee.getAllAngularTestabilities=()=>n.getAllTestabilities(),Ee.getAllAngularRootElements=()=>n.getAllRootElements(),Ee.frameworkStabilizers||(Ee.frameworkStabilizers=[]),Ee.frameworkStabilizers.push(o=>{const i=Ee.getAllAngularTestabilities();let r=i.length;const s=function(){r--,0==r&&o()};i.forEach(a=>{a.whenStable(s)})})}findTestabilityInTree(n,t,o){return null==t?null:n.getTestability(t)??(o?mr().isShadowRoot(t)?this.findTestabilityInTree(n,t.host,!0):this.findTestabilityInTree(n,t.parentElement,!0):null)}}},{provide:Uy,useClass:vf,deps:[ae,yf,sl]},{provide:vf,useClass:vf,deps:[ae,yf,sl]}],ww=[{provide:Uc,useValue:"root"},{provide:ai,useFactory:function cV(){return new ai}},{provide:Dh,useClass:iV,multi:!0,deps:[Ln]},{provide:Dh,useClass:aV,multi:!0,deps:[Ln]},mw,hw,cw,{provide:Vd,useExisting:mw},{provide:class GI{},useClass:oV},[]];let fV=(()=>{class e{constructor(){}static \u0275fac=function(o){return new(o||e)};static \u0275mod=Gn({type:e});static \u0275inj=dn({providers:[...ww,...Dw],imports:[lw,fL]})}return e})();function Qn(e){return this instanceof Qn?(this.v=e,this):new Qn(e)}function Tw(e){if(!Symbol.asyncIterator)throw new TypeError("Symbol.asyncIterator is not defined.");var t,n=e[Symbol.asyncIterator];return n?n.call(e):(e=function xh(e){var n="function"==typeof Symbol&&Symbol.iterator,t=n&&e[n],o=0;if(t)return t.call(e);if(e&&"number"==typeof e.length)return{next:function(){return e&&o>=e.length&&(e=void 0),{value:e&&e[o++],done:!e}}};throw new TypeError(n?"Object is not iterable.":"Symbol.iterator is not defined.")}(e),t={},o("next"),o("throw"),o("return"),t[Symbol.asyncIterator]=function(){return this},t);function o(r){t[r]=e[r]&&function(s){return new Promise(function(a,l){!function i(r,s,a,l){Promise.resolve(l).then(function(c){r({value:c,done:a})},s)}(a,l,(s=e[r](s)).done,s.value)})}}}"function"==typeof SuppressedError&&SuppressedError;const Sw=e=>e&&"number"==typeof e.length&&"function"!=typeof e;function Ow(e){return Re(e?.then)}function Nw(e){return Re(e[Ic])}function Aw(e){return Symbol.asyncIterator&&Re(e?.[Symbol.asyncIterator])}function xw(e){return new TypeError(`You provided ${null!==e&&"object"==typeof e?"an invalid object":`'${e}'`} where a stream was expected. You can provide an Observable, Promise, ReadableStream, Array, AsyncIterable, or Iterable.`)}const kw=function HV(){return"function"==typeof Symbol&&Symbol.iterator?Symbol.iterator:"@@iterator"}();function Rw(e){return Re(e?.[kw])}function Fw(e){return function Mw(e,n,t){if(!Symbol.asyncIterator)throw new TypeError("Symbol.asyncIterator is not defined.");var i,o=t.apply(e,n||[]),r=[];return i=Object.create(("function"==typeof AsyncIterator?AsyncIterator:Object).prototype),a("next"),a("throw"),a("return",function s(h){return function(p){return Promise.resolve(p).then(h,d)}}),i[Symbol.asyncIterator]=function(){return this},i;function a(h,p){o[h]&&(i[h]=function(b){return new Promise(function(M,N){r.push([h,b,M,N])>1||l(h,b)})},p&&(i[h]=p(i[h])))}function l(h,p){try{!function c(h){h.value instanceof Qn?Promise.resolve(h.value.v).then(u,d):g(r[0][2],h)}(o[h](p))}catch(b){g(r[0][3],b)}}function u(h){l("next",h)}function d(h){l("throw",h)}function g(h,p){h(p),r.shift(),r.length&&l(r[0][0],r[0][1])}}(this,arguments,function*(){const t=e.getReader();try{for(;;){const{value:o,done:i}=yield Qn(t.read());if(i)return yield Qn(void 0);yield yield Qn(o)}}finally{t.releaseLock()}})}function Lw(e){return Re(e?.getReader)}function ys(e){if(e instanceof ft)return e;if(null!=e){if(Nw(e))return function BV(e){return new ft(n=>{const t=e[Ic]();if(Re(t.subscribe))return t.subscribe(n);throw new TypeError("Provided object does not correctly implement Symbol.observable")})}(e);if(Sw(e))return function jV(e){return new ft(n=>{for(let t=0;t<e.length&&!n.closed;t++)n.next(e[t]);n.complete()})}(e);if(Ow(e))return function UV(e){return new ft(n=>{e.then(t=>{n.closed||(n.next(t),n.complete())},t=>n.error(t)).then(null,bg)})}(e);if(Aw(e))return Pw(e);if(Rw(e))return function $V(e){return new ft(n=>{for(const t of e)if(n.next(t),n.closed)return;n.complete()})}(e);if(Lw(e))return function zV(e){return Pw(Fw(e))}(e)}throw xw(e)}function Pw(e){return new ft(n=>{(function GV(e,n){var t,o,i,r;return function Ew(e,n,t,o){return new(t||(t=Promise))(function(r,s){function a(u){try{c(o.next(u))}catch(d){s(d)}}function l(u){try{c(o.throw(u))}catch(d){s(d)}}function c(u){u.done?r(u.value):function i(r){return r instanceof t?r:new t(function(s){s(r)})}(u.value).then(a,l)}c((o=o.apply(e,n||[])).next())})}(this,void 0,void 0,function*(){try{for(t=Tw(e);!(o=yield t.next()).done;)if(n.next(o.value),n.closed)return}catch(s){i={error:s}}finally{try{o&&!o.done&&(r=t.return)&&(yield r.call(t))}finally{if(i)throw i.error}}n.complete()})})(e,n).catch(t=>n.error(t))})}function $o(e,n,t,o=0,i=!1){const r=n.schedule(function(){t(),i?e.add(this.schedule(null,o)):this.unsubscribe()},o);if(e.add(r),!i)return r}function Vw(e,n=0){return bo((t,o)=>{t.subscribe(Pn(o,i=>$o(o,e,()=>o.next(i),n),()=>$o(o,e,()=>o.complete(),n),i=>$o(o,e,()=>o.error(i),n)))})}function Hw(e,n=0){return bo((t,o)=>{o.add(e.schedule(()=>t.subscribe(o),n))})}function Bw(e,n){if(!e)throw new Error("Iterable cannot be null");return new ft(t=>{$o(t,n,()=>{const o=e[Symbol.asyncIterator]();$o(t,n,()=>{o.next().then(i=>{i.done?t.complete():t.next(i.value)})},0,!0)})})}const{isArray:XV}=Array,{getPrototypeOf:eH,prototype:tH,keys:nH}=Object;const{isArray:sH}=Array;function cH(e,n){return e.reduce((t,o,i)=>(t[o]=n[i],t),{})}function uH(...e){const n=function rH(e){return Re(function Rh(e){return e[e.length-1]}(e))?e.pop():void 0}(e),{args:t,keys:o}=function oH(e){if(1===e.length){const n=e[0];if(XV(n))return{args:n,keys:null};if(function iH(e){return e&&"object"==typeof e&&eH(e)===tH}(n)){const t=nH(n);return{args:t.map(o=>n[o]),keys:t}}}return{args:e,keys:null}}(e),i=new ft(r=>{const{length:s}=t;if(!s)return void r.complete();const a=new Array(s);let l=s,c=s;for(let u=0;u<s;u++){let d=!1;ys(t[u]).subscribe(Pn(r,g=>{d||(d=!0,c--),a[u]=g},()=>l--,void 0,()=>{(!l||!d)&&(c||r.next(o?cH(o,a):a),r.complete())}))}});return n?i.pipe(function lH(e){return hu(n=>function aH(e,n){return sH(n)?e(...n):e(n)}(e,n))}(n)):i}let jw=(()=>{class e{_renderer;_elementRef;onChange=t=>{};onTouched=()=>{};constructor(t,o){this._renderer=t,this._elementRef=o}setProperty(t,o){this._renderer.setProperty(this._elementRef.nativeElement,t,o)}registerOnTouched(t){this.onTouched=t}registerOnChange(t){this.onChange=t}setDisabledState(t){this.setProperty("disabled",t)}static \u0275fac=function(o){return new(o||e)(T(qt),T(at))};static \u0275dir=G({type:e})}return e})(),zo=(()=>{class e extends jw{static \u0275fac=(()=>{let t;return function(i){return(t||(t=Ze(e)))(i||e)}})();static \u0275dir=G({type:e,features:[oe]})}return e})();const Zt=new R(""),dH={provide:Zt,useExisting:fe(()=>Fh),multi:!0};let Fh=(()=>{class e extends zo{writeValue(t){this.setProperty("checked",t)}static \u0275fac=(()=>{let t;return function(i){return(t||(t=Ze(e)))(i||e)}})();static \u0275dir=G({type:e,selectors:[["input","type","checkbox","formControlName",""],["input","type","checkbox","formControl",""],["input","type","checkbox","ngModel",""]],hostBindings:function(o,i){1&o&&j("change",function(s){return i.onChange(s.target.checked)})("blur",function(){return i.onTouched()})},standalone:!1,features:[be([dH]),oe]})}return e})();const fH={provide:Zt,useExisting:fe(()=>Cs),multi:!0},gH=new R("");let Cs=(()=>{class e extends jw{_compositionMode;_composing=!1;constructor(t,o,i){super(t,o),this._compositionMode=i,null==this._compositionMode&&(this._compositionMode=!function hH(){const e=mr()?mr().getUserAgent():"";return/android (\d+)/.test(e.toLowerCase())}())}writeValue(t){this.setProperty("value",t??"")}_handleInput(t){(!this._compositionMode||this._compositionMode&&!this._composing)&&this.onChange(t)}_compositionStart(){this._composing=!0}_compositionEnd(t){this._composing=!1,this._compositionMode&&this.onChange(t)}static \u0275fac=function(o){return new(o||e)(T(qt),T(at),T(gH,8))};static \u0275dir=G({type:e,selectors:[["input","formControlName","",3,"type","checkbox"],["textarea","formControlName",""],["input","formControl","",3,"type","checkbox"],["textarea","formControl",""],["input","ngModel","",3,"type","checkbox"],["textarea","ngModel",""],["","ngDefaultControl",""]],hostBindings:function(o,i){1&o&&j("input",function(s){return i._handleInput(s.target.value)})("blur",function(){return i.onTouched()})("compositionstart",function(){return i._compositionStart()})("compositionend",function(s){return i._compositionEnd(s.target.value)})},standalone:!1,features:[be([fH]),oe]})}return e})();const et=new R(""),Kn=new R("");function Zw(e){return null!=e}function Qw(e){return al(e)?function KV(e,n){return n?function QV(e,n){if(null!=e){if(Nw(e))return function qV(e,n){return ys(e).pipe(Hw(n),Vw(n))}(e,n);if(Sw(e))return function YV(e,n){return new ft(t=>{let o=0;return n.schedule(function(){o===e.length?t.complete():(t.next(e[o++]),t.closed||this.schedule())})})}(e,n);if(Ow(e))return function WV(e,n){return ys(e).pipe(Hw(n),Vw(n))}(e,n);if(Aw(e))return Bw(e,n);if(Rw(e))return function JV(e,n){return new ft(t=>{let o;return $o(t,n,()=>{o=e[kw](),$o(t,n,()=>{let i,r;try{({value:i,done:r}=o.next())}catch(s){return void t.error(s)}r?t.complete():t.next(i)},0,!0)}),()=>Re(o?.return)&&o.return()})}(e,n);if(Lw(e))return function ZV(e,n){return Bw(Fw(e),n)}(e,n)}throw xw(e)}(e,n):ys(e)}(e):e}function Kw(e){let n={};return e.forEach(t=>{n=null!=t?{...n,...t}:n}),0===Object.keys(n).length?null:n}function Xw(e,n){return n.map(t=>t(e))}function eE(e){return e.map(n=>function mH(e){return!e.validate}(n)?n:t=>n.validate(t))}function Vh(e){return null!=e?function tE(e){if(!e)return null;const n=e.filter(Zw);return 0==n.length?null:function(t){return Kw(Xw(t,n))}}(eE(e)):null}function Hh(e){return null!=e?function nE(e){if(!e)return null;const n=e.filter(Zw);return 0==n.length?null:function(t){return uH(Xw(t,n).map(Qw)).pipe(hu(Kw))}}(eE(e)):null}function oE(e,n){return null===e?[n]:Array.isArray(e)?[...e,n]:[e,n]}function Bh(e){return e?Array.isArray(e)?e:[e]:[]}function Gl(e,n){return Array.isArray(e)?e.includes(n):e===n}function sE(e,n){const t=Bh(n);return Bh(e).forEach(i=>{Gl(t,i)||t.push(i)}),t}function aE(e,n){return Bh(n).filter(t=>!Gl(e,t))}class lE{get value(){return this.control?this.control.value:null}get valid(){return this.control?this.control.valid:null}get invalid(){return this.control?this.control.invalid:null}get pending(){return this.control?this.control.pending:null}get disabled(){return this.control?this.control.disabled:null}get enabled(){return this.control?this.control.enabled:null}get errors(){return this.control?this.control.errors:null}get pristine(){return this.control?this.control.pristine:null}get dirty(){return this.control?this.control.dirty:null}get touched(){return this.control?this.control.touched:null}get status(){return this.control?this.control.status:null}get untouched(){return this.control?this.control.untouched:null}get statusChanges(){return this.control?this.control.statusChanges:null}get valueChanges(){return this.control?this.control.valueChanges:null}get path(){return null}_composedValidatorFn;_composedAsyncValidatorFn;_rawValidators=[];_rawAsyncValidators=[];_setValidators(n){this._rawValidators=n||[],this._composedValidatorFn=Vh(this._rawValidators)}_setAsyncValidators(n){this._rawAsyncValidators=n||[],this._composedAsyncValidatorFn=Hh(this._rawAsyncValidators)}get validator(){return this._composedValidatorFn||null}get asyncValidator(){return this._composedAsyncValidatorFn||null}_onDestroyCallbacks=[];_registerOnDestroy(n){this._onDestroyCallbacks.push(n)}_invokeOnDestroyCallbacks(){this._onDestroyCallbacks.forEach(n=>n()),this._onDestroyCallbacks=[]}reset(n=void 0){this.control&&this.control.reset(n)}hasError(n,t){return!!this.control&&this.control.hasError(n,t)}getError(n,t){return this.control?this.control.getError(n,t):null}}class dt extends lE{name;get formDirective(){return null}get path(){return null}}class Xn extends lE{_parent=null;name=null;valueAccessor=null}class cE{_cd;constructor(n){this._cd=n}get isTouched(){return this._cd?.control?._touched?.(),!!this._cd?.control?.touched}get isUntouched(){return!!this._cd?.control?.untouched}get isPristine(){return this._cd?.control?._pristine?.(),!!this._cd?.control?.pristine}get isDirty(){return!!this._cd?.control?.dirty}get isValid(){return this._cd?.control?._status?.(),!!this._cd?.control?.valid}get isInvalid(){return!!this._cd?.control?.invalid}get isPending(){return!!this._cd?.control?.pending}get isSubmitted(){return this._cd?._submitted?.(),!!this._cd?.submitted}}let ql=(()=>{class e extends cE{constructor(t){super(t)}static \u0275fac=function(o){return new(o||e)(T(Xn,2))};static \u0275dir=G({type:e,selectors:[["","formControlName",""],["","ngModel",""],["","formControl",""]],hostVars:14,hostBindings:function(o,i){2&o&&En("ng-untouched",i.isUntouched)("ng-touched",i.isTouched)("ng-pristine",i.isPristine)("ng-dirty",i.isDirty)("ng-valid",i.isValid)("ng-invalid",i.isInvalid)("ng-pending",i.isPending)},standalone:!1,features:[oe]})}return e})();const bs="VALID",Yl="INVALID",Wi="PENDING",Ds="DISABLED";class Yi{}class dE extends Yi{value;source;constructor(n,t){super(),this.value=n,this.source=t}}class $h extends Yi{pristine;source;constructor(n,t){super(),this.pristine=n,this.source=t}}class zh extends Yi{touched;source;constructor(n,t){super(),this.touched=n,this.source=t}}class Jl extends Yi{status;source;constructor(n,t){super(),this.status=n,this.source=t}}function Zl(e){return null!=e&&!Array.isArray(e)&&"object"==typeof e}class Wh{_pendingDirty=!1;_hasOwnPendingAsyncValidator=null;_pendingTouched=!1;_onCollectionChange=()=>{};_updateOn;_parent=null;_asyncValidationSubscription;_composedValidatorFn;_composedAsyncValidatorFn;_rawValidators;_rawAsyncValidators;value;constructor(n,t){this._assignValidators(n),this._assignAsyncValidators(t)}get validator(){return this._composedValidatorFn}set validator(n){this._rawValidators=this._composedValidatorFn=n}get asyncValidator(){return this._composedAsyncValidatorFn}set asyncValidator(n){this._rawAsyncValidators=this._composedAsyncValidatorFn=n}get parent(){return this._parent}get status(){return Be(this.statusReactive)}set status(n){Be(()=>this.statusReactive.set(n))}_status=ln(()=>this.statusReactive());statusReactive=Co(void 0);get valid(){return this.status===bs}get invalid(){return this.status===Yl}get pending(){return this.status==Wi}get disabled(){return this.status===Ds}get enabled(){return this.status!==Ds}errors;get pristine(){return Be(this.pristineReactive)}set pristine(n){Be(()=>this.pristineReactive.set(n))}_pristine=ln(()=>this.pristineReactive());pristineReactive=Co(!0);get dirty(){return!this.pristine}get touched(){return Be(this.touchedReactive)}set touched(n){Be(()=>this.touchedReactive.set(n))}_touched=ln(()=>this.touchedReactive());touchedReactive=Co(!1);get untouched(){return!this.touched}_events=new Kt;events=this._events.asObservable();valueChanges;statusChanges;get updateOn(){return this._updateOn?this._updateOn:this.parent?this.parent.updateOn:"change"}setValidators(n){this._assignValidators(n)}setAsyncValidators(n){this._assignAsyncValidators(n)}addValidators(n){this.setValidators(sE(n,this._rawValidators))}addAsyncValidators(n){this.setAsyncValidators(sE(n,this._rawAsyncValidators))}removeValidators(n){this.setValidators(aE(n,this._rawValidators))}removeAsyncValidators(n){this.setAsyncValidators(aE(n,this._rawAsyncValidators))}hasValidator(n){return Gl(this._rawValidators,n)}hasAsyncValidator(n){return Gl(this._rawAsyncValidators,n)}clearValidators(){this.validator=null}clearAsyncValidators(){this.asyncValidator=null}markAsTouched(n={}){const t=!1===this.touched;this.touched=!0;const o=n.sourceControl??this;this._parent&&!n.onlySelf&&this._parent.markAsTouched({...n,sourceControl:o}),t&&!1!==n.emitEvent&&this._events.next(new zh(!0,o))}markAllAsDirty(n={}){this.markAsDirty({onlySelf:!0,emitEvent:n.emitEvent,sourceControl:this}),this._forEachChild(t=>t.markAllAsDirty(n))}markAllAsTouched(n={}){this.markAsTouched({onlySelf:!0,emitEvent:n.emitEvent,sourceControl:this}),this._forEachChild(t=>t.markAllAsTouched(n))}markAsUntouched(n={}){const t=!0===this.touched;this.touched=!1,this._pendingTouched=!1;const o=n.sourceControl??this;this._forEachChild(i=>{i.markAsUntouched({onlySelf:!0,emitEvent:n.emitEvent,sourceControl:o})}),this._parent&&!n.onlySelf&&this._parent._updateTouched(n,o),t&&!1!==n.emitEvent&&this._events.next(new zh(!1,o))}markAsDirty(n={}){const t=!0===this.pristine;this.pristine=!1;const o=n.sourceControl??this;this._parent&&!n.onlySelf&&this._parent.markAsDirty({...n,sourceControl:o}),t&&!1!==n.emitEvent&&this._events.next(new $h(!1,o))}markAsPristine(n={}){const t=!1===this.pristine;this.pristine=!0,this._pendingDirty=!1;const o=n.sourceControl??this;this._forEachChild(i=>{i.markAsPristine({onlySelf:!0,emitEvent:n.emitEvent})}),this._parent&&!n.onlySelf&&this._parent._updatePristine(n,o),t&&!1!==n.emitEvent&&this._events.next(new $h(!0,o))}markAsPending(n={}){this.status=Wi;const t=n.sourceControl??this;!1!==n.emitEvent&&(this._events.next(new Jl(this.status,t)),this.statusChanges.emit(this.status)),this._parent&&!n.onlySelf&&this._parent.markAsPending({...n,sourceControl:t})}disable(n={}){const t=this._parentMarkedDirty(n.onlySelf);this.status=Ds,this.errors=null,this._forEachChild(i=>{i.disable({...n,onlySelf:!0})}),this._updateValue();const o=n.sourceControl??this;!1!==n.emitEvent&&(this._events.next(new dE(this.value,o)),this._events.next(new Jl(this.status,o)),this.valueChanges.emit(this.value),this.statusChanges.emit(this.status)),this._updateAncestors({...n,skipPristineCheck:t},this),this._onDisabledChange.forEach(i=>i(!0))}enable(n={}){const t=this._parentMarkedDirty(n.onlySelf);this.status=bs,this._forEachChild(o=>{o.enable({...n,onlySelf:!0})}),this.updateValueAndValidity({onlySelf:!0,emitEvent:n.emitEvent}),this._updateAncestors({...n,skipPristineCheck:t},this),this._onDisabledChange.forEach(o=>o(!1))}_updateAncestors(n,t){this._parent&&!n.onlySelf&&(this._parent.updateValueAndValidity(n),n.skipPristineCheck||this._parent._updatePristine({},t),this._parent._updateTouched({},t))}setParent(n){this._parent=n}getRawValue(){return this.value}updateValueAndValidity(n={}){if(this._setInitialStatus(),this._updateValue(),this.enabled){const o=this._cancelExistingSubscription();this.errors=this._runValidator(),this.status=this._calculateStatus(),(this.status===bs||this.status===Wi)&&this._runAsyncValidator(o,n.emitEvent)}const t=n.sourceControl??this;!1!==n.emitEvent&&(this._events.next(new dE(this.value,t)),this._events.next(new Jl(this.status,t)),this.valueChanges.emit(this.value),this.statusChanges.emit(this.status)),this._parent&&!n.onlySelf&&this._parent.updateValueAndValidity({...n,sourceControl:t})}_updateTreeValidity(n={emitEvent:!0}){this._forEachChild(t=>t._updateTreeValidity(n)),this.updateValueAndValidity({onlySelf:!0,emitEvent:n.emitEvent})}_setInitialStatus(){this.status=this._allControlsDisabled()?Ds:bs}_runValidator(){return this.validator?this.validator(this):null}_runAsyncValidator(n,t){if(this.asyncValidator){this.status=Wi,this._hasOwnPendingAsyncValidator={emitEvent:!1!==t,shouldHaveEmitted:!1!==n};const o=Qw(this.asyncValidator(this));this._asyncValidationSubscription=o.subscribe(i=>{this._hasOwnPendingAsyncValidator=null,this.setErrors(i,{emitEvent:t,shouldHaveEmitted:n})})}}_cancelExistingSubscription(){if(this._asyncValidationSubscription){this._asyncValidationSubscription.unsubscribe();const n=(this._hasOwnPendingAsyncValidator?.emitEvent||this._hasOwnPendingAsyncValidator?.shouldHaveEmitted)??!1;return this._hasOwnPendingAsyncValidator=null,n}return!1}setErrors(n,t={}){this.errors=n,this._updateControlsErrors(!1!==t.emitEvent,this,t.shouldHaveEmitted)}get(n){let t=n;return null==t||(Array.isArray(t)||(t=t.split(".")),0===t.length)?null:t.reduce((o,i)=>o&&o._find(i),this)}getError(n,t){const o=t?this.get(t):this;return o&&o.errors?o.errors[n]:null}hasError(n,t){return!!this.getError(n,t)}get root(){let n=this;for(;n._parent;)n=n._parent;return n}_updateControlsErrors(n,t,o){this.status=this._calculateStatus(),n&&this.statusChanges.emit(this.status),(n||o)&&this._events.next(new Jl(this.status,t)),this._parent&&this._parent._updateControlsErrors(n,t,o)}_initObservables(){this.valueChanges=new me,this.statusChanges=new me}_calculateStatus(){return this._allControlsDisabled()?Ds:this.errors?Yl:this._hasOwnPendingAsyncValidator||this._anyControlsHaveStatus(Wi)?Wi:this._anyControlsHaveStatus(Yl)?Yl:bs}_anyControlsHaveStatus(n){return this._anyControls(t=>t.status===n)}_anyControlsDirty(){return this._anyControls(n=>n.dirty)}_anyControlsTouched(){return this._anyControls(n=>n.touched)}_updatePristine(n,t){const o=!this._anyControlsDirty(),i=this.pristine!==o;this.pristine=o,this._parent&&!n.onlySelf&&this._parent._updatePristine(n,t),i&&this._events.next(new $h(this.pristine,t))}_updateTouched(n={},t){this.touched=this._anyControlsTouched(),this._events.next(new zh(this.touched,t)),this._parent&&!n.onlySelf&&this._parent._updateTouched(n,t)}_onDisabledChange=[];_registerOnCollectionChange(n){this._onCollectionChange=n}_setUpdateStrategy(n){Zl(n)&&null!=n.updateOn&&(this._updateOn=n.updateOn)}_parentMarkedDirty(n){return!n&&!(!this._parent||!this._parent.dirty)&&!this._parent._anyControlsDirty()}_find(n){return null}_assignValidators(n){this._rawValidators=Array.isArray(n)?n.slice():n,this._composedValidatorFn=function EH(e){return Array.isArray(e)?Vh(e):e||null}(this._rawValidators)}_assignAsyncValidators(n){this._rawAsyncValidators=Array.isArray(n)?n.slice():n,this._composedAsyncValidatorFn=function IH(e){return Array.isArray(e)?Hh(e):e||null}(this._rawAsyncValidators)}}const Ji=new R("",{providedIn:"root",factory:()=>Ql}),Ql="always";function ws(e,n,t=Ql){(function Jh(e,n){const t=function iE(e){return e._rawValidators}(e);null!==n.validator?e.setValidators(oE(t,n.validator)):"function"==typeof t&&e.setValidators([t]);const o=function rE(e){return e._rawAsyncValidators}(e);null!==n.asyncValidator?e.setAsyncValidators(oE(o,n.asyncValidator)):"function"==typeof o&&e.setAsyncValidators([o]);const i=()=>e.updateValueAndValidity();ec(n._rawValidators,i),ec(n._rawAsyncValidators,i)})(e,n),n.valueAccessor.writeValue(e.value),(e.disabled||"always"===t)&&n.valueAccessor.setDisabledState?.(e.disabled),function SH(e,n){n.valueAccessor.registerOnChange(t=>{e._pendingValue=t,e._pendingChange=!0,e._pendingDirty=!0,"change"===e.updateOn&&mE(e,n)})}(e,n),function NH(e,n){const t=(o,i)=>{n.valueAccessor.writeValue(o),i&&n.viewToModelUpdate(o)};e.registerOnChange(t),n._registerOnDestroy(()=>{e._unregisterOnChange(t)})}(e,n),function OH(e,n){n.valueAccessor.registerOnTouched(()=>{e._pendingTouched=!0,"blur"===e.updateOn&&e._pendingChange&&mE(e,n),"submit"!==e.updateOn&&e.markAsTouched()})}(e,n),function TH(e,n){if(n.valueAccessor.setDisabledState){const t=o=>{n.valueAccessor.setDisabledState(o)};e.registerOnDisabledChange(t),n._registerOnDestroy(()=>{e._unregisterOnDisabledChange(t)})}}(e,n)}function ec(e,n){e.forEach(t=>{t.registerOnValidatorChange&&t.registerOnValidatorChange(n)})}function mE(e,n){e._pendingDirty&&e.markAsDirty(),e.setValue(e._pendingValue,{emitModelToViewChange:!1}),n.viewToModelUpdate(e._pendingValue),e._pendingChange=!1}function yE(e,n){const t=e.indexOf(n);t>-1&&e.splice(t,1)}function CE(e){return"object"==typeof e&&null!==e&&2===Object.keys(e).length&&"value"in e&&"disabled"in e}Promise.resolve();const bE=class extends Wh{defaultValue=null;_onChange=[];_pendingValue;_pendingChange=!1;constructor(n=null,t,o){super(function Gh(e){return(Zl(e)?e.validators:e)||null}(t),function qh(e,n){return(Zl(n)?n.asyncValidators:e)||null}(o,t)),this._applyFormState(n),this._setUpdateStrategy(t),this._initObservables(),this.updateValueAndValidity({onlySelf:!0,emitEvent:!!this.asyncValidator}),Zl(t)&&(t.nonNullable||t.initialValueIsDefault)&&(this.defaultValue=CE(n)?n.value:n)}setValue(n,t={}){this.value=this._pendingValue=n,this._onChange.length&&!1!==t.emitModelToViewChange&&this._onChange.forEach(o=>o(this.value,!1!==t.emitViewToModelChange)),this.updateValueAndValidity(t)}patchValue(n,t={}){this.setValue(n,t)}reset(n=this.defaultValue,t={}){this._applyFormState(n),this.markAsPristine(t),this.markAsUntouched(t),this.setValue(this.value,t),this._pendingChange=!1}_updateValue(){}_anyControls(n){return!1}_allControlsDisabled(){return this.disabled}registerOnChange(n){this._onChange.push(n)}_unregisterOnChange(n){yE(this._onChange,n)}registerOnDisabledChange(n){this._onDisabledChange.push(n)}_unregisterOnDisabledChange(n){yE(this._onDisabledChange,n)}_forEachChild(n){}_syncPendingControls(){return!("submit"!==this.updateOn||(this._pendingDirty&&this.markAsDirty(),this._pendingTouched&&this.markAsTouched(),!this._pendingChange)||(this.setValue(this._pendingValue,{onlySelf:!0,emitModelToViewChange:!1}),0))}_applyFormState(n){CE(n)?(this.value=this._pendingValue=n.value,n.disabled?this.disable({onlySelf:!0,emitEvent:!1}):this.enable({onlySelf:!0,emitEvent:!1})):this.value=this._pendingValue=n}},BH={provide:Xn,useExisting:fe(()=>Is)},DE=Promise.resolve();let Is=(()=>{class e extends Xn{_changeDetectorRef;callSetDisabledState;control=new bE;static ngAcceptInputType_isDisabled;_registered=!1;viewModel;name="";isDisabled;model;options;update=new me;constructor(t,o,i,r,s,a){super(),this._changeDetectorRef=s,this.callSetDisabledState=a,this._parent=t,this._setValidators(o),this._setAsyncValidators(i),this.valueAccessor=function Kh(e,n){if(!n)return null;let t,o,i;return Array.isArray(n),n.forEach(r=>{r.constructor===Cs?t=r:function kH(e){return Object.getPrototypeOf(e.constructor)===zo}(r)?o=r:i=r}),i||o||t||null}(0,r)}ngOnChanges(t){if(this._checkForErrors(),!this._registered||"name"in t){if(this._registered&&(this._checkName(),this.formDirective)){const o=t.name.previousValue;this.formDirective.removeControl({name:o,path:this._getPath(o)})}this._setUpControl()}"isDisabled"in t&&this._updateDisabled(t),function Qh(e,n){if(!e.hasOwnProperty("model"))return!1;const t=e.model;return!!t.isFirstChange()||!Object.is(n,t.currentValue)}(t,this.viewModel)&&(this._updateValue(this.model),this.viewModel=this.model)}ngOnDestroy(){this.formDirective&&this.formDirective.removeControl(this)}get path(){return this._getPath(this.name)}get formDirective(){return this._parent?this._parent.formDirective:null}viewToModelUpdate(t){this.viewModel=t,this.update.emit(t)}_setUpControl(){this._setUpdateStrategy(),this._isStandalone()?this._setUpStandalone():this.formDirective.addControl(this),this._registered=!0}_setUpdateStrategy(){this.options&&null!=this.options.updateOn&&(this.control._updateOn=this.options.updateOn)}_isStandalone(){return!this._parent||!(!this.options||!this.options.standalone)}_setUpStandalone(){ws(this.control,this,this.callSetDisabledState),this.control.updateValueAndValidity({emitEvent:!1})}_checkForErrors(){this._checkName()}_checkName(){this.options&&this.options.name&&(this.name=this.options.name),this._isStandalone()}_updateValue(t){DE.then(()=>{this.control.setValue(t,{emitViewToModelChange:!1}),this._changeDetectorRef?.markForCheck()})}_updateDisabled(t){const o=t.isDisabled.currentValue,i=0!==o&&function th(e){return"boolean"==typeof e?e:null!=e&&"false"!==e}(o);DE.then(()=>{i&&!this.control.disabled?this.control.disable():!i&&this.control.disabled&&this.control.enable(),this._changeDetectorRef?.markForCheck()})}_getPath(t){return this._parent?function Kl(e,n){return[...n.path,e]}(t,this._parent):[t]}static \u0275fac=function(o){return new(o||e)(T(dt,9),T(et,10),T(Kn,10),T(Zt,10),T(zi,8),T(Ji,8))};static \u0275dir=G({type:e,selectors:[["","ngModel","",3,"formControlName","",3,"formControl",""]],inputs:{name:"name",isDisabled:[0,"disabled","isDisabled"],model:[0,"ngModel","model"],options:[0,"ngModelOptions","options"]},outputs:{update:"ngModelChange"},exportAs:["ngModel"],standalone:!1,features:[be([BH]),oe,Cn]})}return e})();const GH={provide:Zt,useExisting:fe(()=>Xh),multi:!0};let Xh=(()=>{class e extends zo{writeValue(t){this.setProperty("value",parseFloat(t))}registerOnChange(t){this.onChange=o=>{t(""==o?null:parseFloat(o))}}static \u0275fac=(()=>{let t;return function(i){return(t||(t=Ze(e)))(i||e)}})();static \u0275dir=G({type:e,selectors:[["input","type","range","formControlName",""],["input","type","range","formControl",""],["input","type","range","ngModel",""]],hostBindings:function(o,i){1&o&&j("change",function(s){return i.onChange(s.target.value)})("input",function(s){return i.onChange(s.target.value)})("blur",function(){return i.onTouched()})},standalone:!1,features:[be([GH]),oe]})}return e})();const QH={provide:Zt,useExisting:fe(()=>Ts),multi:!0};function OE(e,n){return null==e?`${n}`:(n&&"object"==typeof n&&(n="Object"),`${e}: ${n}`.slice(0,50))}let Ts=(()=>{class e extends zo{value;_optionMap=new Map;_idCounter=0;set compareWith(t){this._compareWith=t}_compareWith=Object.is;writeValue(t){this.value=t;const i=OE(this._getOptionId(t),t);this.setProperty("value",i)}registerOnChange(t){this.onChange=o=>{this.value=this._getOptionValue(o),t(this.value)}}_registerOption(){return(this._idCounter++).toString()}_getOptionId(t){for(const o of this._optionMap.keys())if(this._compareWith(this._optionMap.get(o),t))return o;return null}_getOptionValue(t){const o=function KH(e){return e.split(":")[0]}(t);return this._optionMap.has(o)?this._optionMap.get(o):t}static \u0275fac=(()=>{let t;return function(i){return(t||(t=Ze(e)))(i||e)}})();static \u0275dir=G({type:e,selectors:[["select","formControlName","",3,"multiple",""],["select","formControl","",3,"multiple",""],["select","ngModel","",3,"multiple",""]],hostBindings:function(o,i){1&o&&j("change",function(s){return i.onChange(s.target.value)})("blur",function(){return i.onTouched()})},inputs:{compareWith:"compareWith"},standalone:!1,features:[be([QH]),oe]})}return e})(),eg=(()=>{class e{_element;_renderer;_select;id;constructor(t,o,i){this._element=t,this._renderer=o,this._select=i,this._select&&(this.id=this._select._registerOption())}set ngValue(t){null!=this._select&&(this._select._optionMap.set(this.id,t),this._setElementValue(OE(this.id,t)),this._select.writeValue(this._select.value))}set value(t){this._setElementValue(t),this._select&&this._select.writeValue(this._select.value)}_setElementValue(t){this._renderer.setProperty(this._element.nativeElement,"value",t)}ngOnDestroy(){this._select&&(this._select._optionMap.delete(this.id),this._select.writeValue(this._select.value))}static \u0275fac=function(o){return new(o||e)(T(at),T(qt),T(Ts,9))};static \u0275dir=G({type:e,selectors:[["option"]],inputs:{ngValue:"ngValue",value:"value"},standalone:!1})}return e})();const XH={provide:Zt,useExisting:fe(()=>tg),multi:!0};function NE(e,n){return null==e?`${n}`:("string"==typeof n&&(n=`'${n}'`),n&&"object"==typeof n&&(n="Object"),`${e}: ${n}`.slice(0,50))}let tg=(()=>{class e extends zo{value;_optionMap=new Map;_idCounter=0;set compareWith(t){this._compareWith=t}_compareWith=Object.is;writeValue(t){let o;if(this.value=t,Array.isArray(t)){const i=t.map(r=>this._getOptionId(r));o=(r,s)=>{r._setSelected(i.indexOf(s.toString())>-1)}}else o=(i,r)=>{i._setSelected(!1)};this._optionMap.forEach(o)}registerOnChange(t){this.onChange=o=>{const i=[],r=o.selectedOptions;if(void 0!==r){const s=r;for(let a=0;a<s.length;a++){const c=this._getOptionValue(s[a].value);i.push(c)}}else{const s=o.options;for(let a=0;a<s.length;a++){const l=s[a];if(l.selected){const c=this._getOptionValue(l.value);i.push(c)}}}this.value=i,t(i)}}_registerOption(t){const o=(this._idCounter++).toString();return this._optionMap.set(o,t),o}_getOptionId(t){for(const o of this._optionMap.keys())if(this._compareWith(this._optionMap.get(o)._value,t))return o;return null}_getOptionValue(t){const o=function eB(e){return e.split(":")[0]}(t);return this._optionMap.has(o)?this._optionMap.get(o)._value:t}static \u0275fac=(()=>{let t;return function(i){return(t||(t=Ze(e)))(i||e)}})();static \u0275dir=G({type:e,selectors:[["select","multiple","","formControlName",""],["select","multiple","","formControl",""],["select","multiple","","ngModel",""]],hostBindings:function(o,i){1&o&&j("change",function(s){return i.onChange(s.target)})("blur",function(){return i.onTouched()})},inputs:{compareWith:"compareWith"},standalone:!1,features:[be([XH]),oe]})}return e})(),ng=(()=>{class e{_element;_renderer;_select;id;_value;constructor(t,o,i){this._element=t,this._renderer=o,this._select=i,this._select&&(this.id=this._select._registerOption(this))}set ngValue(t){null!=this._select&&(this._value=t,this._setElementValue(NE(this.id,t)),this._select.writeValue(this._select.value))}set value(t){this._select?(this._value=t,this._setElementValue(NE(this.id,t)),this._select.writeValue(this._select.value)):this._setElementValue(t)}_setElementValue(t){this._renderer.setProperty(this._element.nativeElement,"value",t)}_setSelected(t){this._renderer.setProperty(this._element.nativeElement,"selected",t)}ngOnDestroy(){this._select&&(this._select._optionMap.delete(this.id),this._select.writeValue(this._select.value))}static \u0275fac=function(o){return new(o||e)(T(at),T(qt),T(tg,9))};static \u0275dir=G({type:e,selectors:[["option"]],inputs:{ngValue:"ngValue",value:"value"},standalone:!1})}return e})(),cB=(()=>{class e{static \u0275fac=function(o){return new(o||e)};static \u0275mod=Gn({type:e});static \u0275inj=dn({})}return e})(),dB=(()=>{class e{static withConfig(t){return{ngModule:e,providers:[{provide:Ji,useValue:t.callSetDisabledState??Ql}]}}static \u0275fac=function(o){return new(o||e)};static \u0275mod=Gn({type:e});static \u0275inj=dn({imports:[cB]})}return e})();class fB extends Et{constructor(n,t){super()}schedule(n,t=0){return this}}const ac={setInterval(e,n,...t){const{delegate:o}=ac;return o?.setInterval?o.setInterval(e,n,...t):setInterval(e,n,...t)},clearInterval(e){const{delegate:n}=ac;return(n?.clearInterval||clearInterval)(e)},delegate:void 0},BE={now:()=>(BE.delegate||Date).now(),delegate:void 0};class Ss{constructor(n,t=Ss.now){this.schedulerActionCtor=n,this.now=t}schedule(n,t=0,o){return new this.schedulerActionCtor(this,n).schedule(o,t)}}Ss.now=BE.now;const jE=new class gB extends Ss{constructor(n,t=Ss.now){super(n,t),this.actions=[],this._active=!1}flush(n){const{actions:t}=this;if(this._active)return void t.push(n);let o;this._active=!0;do{if(o=n.execute(n.state,n.delay))break}while(n=t.shift());if(this._active=!1,o){for(;n=t.shift();)n.unsubscribe();throw o}}}(class hB extends fB{constructor(n,t){super(n,t),this.scheduler=n,this.work=t,this.pending=!1}schedule(n,t=0){var o;if(this.closed)return this;this.state=n;const i=this.id,r=this.scheduler;return null!=i&&(this.id=this.recycleAsyncId(r,i,t)),this.pending=!0,this.delay=t,this.id=null!==(o=this.id)&&void 0!==o?o:this.requestAsyncId(r,this.id,t),this}requestAsyncId(n,t,o=0){return ac.setInterval(n.flush.bind(n,this),o)}recycleAsyncId(n,t,o=0){if(null!=o&&this.delay===o&&!1===this.pending)return t;null!=t&&ac.clearInterval(t)}execute(n,t){if(this.closed)return new Error("executing a cancelled action");this.pending=!1;const o=this._execute(n,t);if(o)return o;!1===this.pending&&null!=this.id&&(this.id=this.recycleAsyncId(this.scheduler,this.id,null))}_execute(n,t){let i,o=!1;try{this.work(n)}catch(r){o=!0,i=r||new Error("Scheduled action threw falsy error")}if(o)return this.unsubscribe(),i}unsubscribe(){if(!this.closed){const{id:n,scheduler:t}=this,{actions:o}=t;this.work=this.state=this.scheduler=null,this.pending=!1,Vs(o,this),null!=n&&(this.id=this.recycleAsyncId(t,n,null)),this.delay=null,super.unsubscribe()}}}),pB=jE;function UE(e,n=jE,t){const o=function yB(e=0,n,t=pB){let o=-1;return null!=n&&(function _B(e){return e&&Re(e.schedule)}(n)?t=n:o=n),new ft(i=>{let r=function vB(e){return e instanceof Date&&!isNaN(e)}(e)?+e-t.now():e;r<0&&(r=0);let s=0;return t.schedule(function(){i.closed||(i.next(s++),0<=o?this.schedule(void 0,o):i.complete())},r)})}(e,n);return function mB(e,n){return bo((t,o)=>{const{leading:i=!0,trailing:r=!1}=n??{};let s=!1,a=null,l=null,c=!1;const u=()=>{l?.unsubscribe(),l=null,r&&(h(),c&&o.complete())},d=()=>{l=null,c&&o.complete()},g=p=>l=ys(e(p)).subscribe(Pn(o,u,d)),h=()=>{if(s){s=!1;const p=a;a=null,o.next(p),!c&&g(p)}};t.subscribe(Pn(o,p=>{s=!0,a=p,(!l||l.closed)&&(i?h():g(p))},()=>{c=!0,(!(r&&s&&l)||l.closed)&&o.complete()}))})}(()=>o,t)}function $E(e,n,t){const o=Re(e)||n||t?{next:e,error:n,complete:t}:e;return o?bo((i,r)=>{var s;null===(s=o.subscribe)||void 0===s||s.call(o);let a=!0;i.subscribe(Pn(r,l=>{var c;null===(c=o.next)||void 0===c||c.call(o,l),r.next(l)},()=>{var l;a=!1,null===(l=o.complete)||void 0===l||l.call(o),r.complete()},l=>{var c;a=!1,null===(c=o.error)||void 0===c||c.call(o,l),r.error(l)},()=>{var l,c;a&&(null===(l=o.unsubscribe)||void 0===l||l.call(o)),null===(c=o.finalize)||void 0===c||c.call(o)}))}):Mc}function zE(e,n=Mc){return e=e??CB,bo((t,o)=>{let i,r=!0;t.subscribe(Pn(o,s=>{const a=n(s);(r||!e(i,a))&&(r=!1,i=a,o.next(s))}))})}function CB(e,n){return e===n}var At=typeof window<"u"?window:{screen:{},navigator:{}},Zi=(At.matchMedia||function(){return{matches:!1}}).bind(At),GE=!1,qE=function(){};At.addEventListener&&At.addEventListener("p",qE,{get passive(){return GE=!0}}),At.removeEventListener&&At.removeEventListener("p",qE,!1);var WE=GE,ig="ontouchstart"in At,JE=(ig||"TouchEvent"in At&&Zi("(any-pointer: coarse)"),At.navigator.userAgent||"");Zi("(pointer: coarse)").matches&&/iPad|Macintosh/.test(JE)&&Math.min(At.screen.width||0,At.screen.height||0);(Zi("(pointer: coarse)").matches||!Zi("(pointer: fine)").matches&&ig)&&/Windows.*Firefox/.test(JE),Zi("(any-pointer: fine)").matches||Zi("(any-hover: hover)");const TB=(e,n,t)=>({tooltip:e,placement:n,content:t});function SB(e,n){}function OB(e,n){1&e&&Ro(0,SB,0,0,"ng-template")}function NB(e,n){if(1&e&&(ts(0),Ro(1,OB,1,0,null,1),ns()),2&e){const t=m();f(),S("ngTemplateOutlet",t.template)("ngTemplateOutletContext",Ne(2,TB,t.tooltip,t.placement,t.content))}}function AB(e,n){if(1&e&&(ts(0),v(1,"div",2),D(2),_(),ns()),2&e){const t=m();f(),lt("title",t.tooltip)("data-tooltip-placement",t.placement),f(),L(" ",t.content," ")}}const xB=["tooltipTemplate"],kB=["leftOuterSelectionBar"],RB=["rightOuterSelectionBar"],FB=["fullBar"],LB=["selectionBar"],PB=["minHandle"],VB=["maxHandle"],HB=["floorLabel"],BB=["ceilLabel"],jB=["minHandleLabel"],UB=["maxHandleLabel"],$B=["combinedLabel"],zB=["ticksElement"],GB=e=>({"ngx-slider-selected":e});function qB(e,n){if(1&e&&x(0,"ngx-slider-tooltip-wrapper",32),2&e){const t=m().$implicit;S("template",m().tooltipTemplate)("tooltip",t.valueTooltip)("placement",t.valueTooltipPlacement)("content",t.value)}}function WB(e,n){1&e&&x(0,"span",33),2&e&&S("innerText",m().$implicit.legend)}function YB(e,n){1&e&&x(0,"span",34),2&e&&S("innerHTML",m().$implicit.legend,r_)}function JB(e,n){if(1&e&&(v(0,"span",27),x(1,"ngx-slider-tooltip-wrapper",28),Ro(2,qB,1,4,"ngx-slider-tooltip-wrapper",29)(3,WB,1,1,"span",30)(4,YB,1,1,"span",31),_()),2&e){const t=n.$implicit,o=m();S("ngClass",ji(8,GB,t.selected))("ngStyle",t.style),f(),S("template",o.tooltipTemplate)("tooltip",t.tooltip)("placement",t.tooltipPlacement),f(),S("ngIf",null!=t.value),f(),S("ngIf",null!=t.legend&&!1===o.allowUnsafeHtmlInSlider),f(),S("ngIf",null!=t.legend&&(null==o.allowUnsafeHtmlInSlider||o.allowUnsafeHtmlInSlider))}}var cn=function(e){return e[e.Low=0]="Low",e[e.High=1]="High",e[e.Floor=2]="Floor",e[e.Ceil=3]="Ceil",e[e.TickValue=4]="TickValue",e}(cn||{});class lc{floor=0;ceil=null;step=1;minRange=null;maxRange=null;pushRange=!1;minLimit=null;maxLimit=null;translate=null;combineLabels=null;getLegend=null;getStepLegend=null;stepsArray=null;bindIndexForStepsArray=!1;draggableRange=!1;draggableRangeOnly=!1;showSelectionBar=!1;showSelectionBarEnd=!1;showSelectionBarFromValue=null;showOuterSelectionBars=!1;hidePointerLabels=!1;hideLimitLabels=!1;autoHideLimitLabels=!0;readOnly=!1;disabled=!1;showTicks=!1;showTicksValues=!1;tickStep=null;tickValueStep=null;ticksArray=null;ticksTooltip=null;ticksValuesTooltip=null;vertical=!1;getSelectionBarColor=null;getTickColor=null;getPointerColor=null;keyboardSupport=!0;scale=1;rotate=0;enforceStep=!0;enforceRange=!0;enforceStepsArray=!0;noSwitching=!1;onlyBindHandles=!1;rightToLeft=!1;reversedControls=!1;boundPointerLabels=!0;logScale=!1;customValueToPosition=null;customPositionToValue=null;precisionLimit=12;selectionBarGradient=null;ariaLabel="ngx-slider";ariaLabelledBy=null;ariaLabelHigh="ngx-slider-max";ariaLabelledByHigh=null;handleDimension=null;barDimension=null;animate=!0;animateOnMove=!1}const KE=new R("AllowUnsafeHtmlInSlider");var F=function(e){return e[e.Min=0]="Min",e[e.Max=1]="Max",e}(F||{});class ZB{value;highValue;pointerType}class I{static isNullOrUndefined(n){return null==n}static areArraysEqual(n,t){if(n.length!==t.length)return!1;for(let o=0;o<n.length;++o)if(n[o]!==t[o])return!1;return!0}static linearValueToPosition(n,t,o){return(n-t)/(o-t)}static logValueToPosition(n,t,o){return((n=Math.log(n))-(t=Math.log(t)))/((o=Math.log(o))-t)}static linearPositionToValue(n,t,o){return n*(o-t)+t}static logPositionToValue(n,t,o){return t=Math.log(t),o=Math.log(o),Math.exp(n*(o-t)+t)}static findStepIndex(n,t){const o=t.map(r=>Math.abs(n-r.value));let i=0;for(let r=0;r<t.length;r++)o[r]!==o[i]&&o[r]<o[i]&&(i=r);return i}}class eo{static isTouchEvent(n){return void 0!==window.TouchEvent?n instanceof TouchEvent:void 0!==n.touches}static isResizeObserverAvailable(){return void 0!==window.ResizeObserver}}class ke{static roundToPrecisionLimit(n,t){return+n.toPrecision(t)}static isModuloWithinPrecisionLimit(n,t,o){const i=Math.pow(10,-o);return Math.abs(n%t)<=i||Math.abs(Math.abs(n%t)-t)<=i}static clampToRange(n,t,o){return Math.min(Math.max(n,t),o)}}class XE{eventName=null;events=null;eventsSubscription=null;teardownCallback=null}class e1{renderer;constructor(n){this.renderer=n}attachPassiveEventListener(n,t,o,i){if(!0!==WE)return this.attachEventListener(n,t,o,i);const r=new XE;r.eventName=t,r.events=new Kt;const s=a=>{r.events.next(a)};return n.addEventListener(t,s,{passive:!0,capture:!1}),r.teardownCallback=()=>{n.removeEventListener(t,s,{passive:!0,capture:!1})},r.eventsSubscription=r.events.pipe(I.isNullOrUndefined(i)?$E(()=>{}):UE(i,void 0,{leading:!0,trailing:!0})).subscribe(a=>{o(a)}),r}detachEventListener(n){I.isNullOrUndefined(n.eventsSubscription)||(n.eventsSubscription.unsubscribe(),n.eventsSubscription=null),I.isNullOrUndefined(n.events)||(n.events.complete(),n.events=null),I.isNullOrUndefined(n.teardownCallback)||(n.teardownCallback(),n.teardownCallback=null)}attachEventListener(n,t,o,i){const r=new XE;return r.eventName=t,r.events=new Kt,r.teardownCallback=this.renderer.listen(n,t,a=>{r.events.next(a)}),r.eventsSubscription=r.events.pipe(I.isNullOrUndefined(i)?$E(()=>{}):UE(i,void 0,{leading:!0,trailing:!0})).subscribe(a=>{o(a)}),r}}let to=(()=>{class e{elemRef;renderer;changeDetectionRef;_position=0;get position(){return this._position}_dimension=0;get dimension(){return this._dimension}_alwaysHide=!1;get alwaysHide(){return this._alwaysHide}_vertical=!1;get vertical(){return this._vertical}_scale=1;get scale(){return this._scale}_rotate=0;get rotate(){return this._rotate}opacity=1;visibility="visible";left="";bottom="";height="";width="";transform="";eventListenerHelper;eventListeners=[];constructor(t,o,i){this.elemRef=t,this.renderer=o,this.changeDetectionRef=i,this.eventListenerHelper=new e1(this.renderer)}setAlwaysHide(t){this._alwaysHide=t,this.visibility=t?"hidden":"visible"}hide(){this.opacity=0}show(){this.alwaysHide||(this.opacity=1)}isVisible(){return!this.alwaysHide&&0!==this.opacity}setVertical(t){this._vertical=t,this._vertical?(this.left="",this.width=""):(this.bottom="",this.height="")}setScale(t){this._scale=t}setRotate(t){this._rotate=t,this.transform="rotate("+t+"deg)"}getRotate(){return this._rotate}setPosition(t){this._position!==t&&!this.isRefDestroyed()&&this.changeDetectionRef.markForCheck(),this._position=t,this._vertical?this.bottom=Math.round(t)+"px":this.left=Math.round(t)+"px"}calculateDimension(){const t=this.getBoundingClientRect();this._dimension=this.vertical?(t.bottom-t.top)*this.scale:(t.right-t.left)*this.scale}setDimension(t){this._dimension!==t&&!this.isRefDestroyed()&&this.changeDetectionRef.markForCheck(),this._dimension=t,this._vertical?this.height=Math.round(t)+"px":this.width=Math.round(t)+"px"}getBoundingClientRect(){return this.elemRef.nativeElement.getBoundingClientRect()}on(t,o,i){const r=this.eventListenerHelper.attachEventListener(this.elemRef.nativeElement,t,o,i);this.eventListeners.push(r)}onPassive(t,o,i){const r=this.eventListenerHelper.attachPassiveEventListener(this.elemRef.nativeElement,t,o,i);this.eventListeners.push(r)}off(t){let o,i;I.isNullOrUndefined(t)?(o=[],i=this.eventListeners):(o=this.eventListeners.filter(r=>r.eventName!==t),i=this.eventListeners.filter(r=>r.eventName===t));for(const r of i)this.eventListenerHelper.detachEventListener(r);this.eventListeners=o}isRefDestroyed(){return I.isNullOrUndefined(this.changeDetectionRef)||this.changeDetectionRef.destroyed}static \u0275fac=function(o){return new(o||e)(T(at),T(qt),T(zi))};static \u0275dir=G({type:e,selectors:[["","ngxSliderElement",""]],hostVars:14,hostBindings:function(o,i){2&o&&pl("opacity",i.opacity)("visibility",i.visibility)("left",i.left)("bottom",i.bottom)("height",i.height)("width",i.width)("transform",i.transform)},standalone:!1})}return e})(),rg=(()=>{class e extends to{active=!1;role="";tabindex="";ariaOrientation="";ariaLabel="";ariaLabelledBy="";ariaValueNow="";ariaValueText="";ariaValueMin="";ariaValueMax="";focus(){this.elemRef.nativeElement.focus()}focusIfNeeded(){document.activeElement!==this.elemRef.nativeElement&&this.elemRef.nativeElement.focus()}constructor(t,o,i){super(t,o,i)}static \u0275fac=function(o){return new(o||e)(T(at),T(qt),T(zi))};static \u0275dir=G({type:e,selectors:[["","ngxSliderHandle",""]],hostVars:11,hostBindings:function(o,i){2&o&&(lt("role",i.role)("tabindex",i.tabindex)("aria-orientation",i.ariaOrientation)("aria-label",i.ariaLabel)("aria-labelledby",i.ariaLabelledBy)("aria-valuenow",i.ariaValueNow)("aria-valuetext",i.ariaValueText)("aria-valuemin",i.ariaValueMin)("aria-valuemax",i.ariaValueMax),En("ngx-slider-active",i.active))},standalone:!1,features:[oe]})}return e})(),Qi=(()=>{class e extends to{allowUnsafeHtmlInSlider;_value=null;get value(){return this._value}constructor(t,o,i,r){super(t,o,i),this.allowUnsafeHtmlInSlider=r}setValue(t){let o=!1;!this.alwaysHide&&(I.isNullOrUndefined(this.value)||this.value.length!==t.length||this.value.length>0&&0===this.dimension)&&(o=!0),this._value=t,!1===this.allowUnsafeHtmlInSlider?this.elemRef.nativeElement.innerText=t:this.elemRef.nativeElement.innerHTML=t,o&&this.calculateDimension()}static \u0275fac=function(o){return new(o||e)(T(at),T(qt),T(zi),T(KE,8))};static \u0275dir=G({type:e,selectors:[["","ngxSliderLabel",""]],standalone:!1,features:[oe]})}return e})(),QB=(()=>{class e{template;tooltip;placement;content;static \u0275fac=function(o){return new(o||e)};static \u0275cmp=Wt({type:e,selectors:[["ngx-slider-tooltip-wrapper"]],inputs:{template:"template",tooltip:"tooltip",placement:"placement",content:"content"},standalone:!1,decls:2,vars:2,consts:[[4,"ngIf"],[4,"ngTemplateOutlet","ngTemplateOutletContext"],[1,"ngx-slider-inner-tooltip"]],template:function(o,i){1&o&&Ro(0,NB,2,6,"ng-container",0)(1,AB,3,3,"ng-container",0),2&o&&(S("ngIf",i.template),f(),S("ngIf",!i.template))},dependencies:[_h,rw],styles:[".ngx-slider-inner-tooltip[_ngcontent-%COMP%]{height:100%}"]})}return e})();class KB{selected=!1;style={};tooltip=null;tooltipPlacement=null;value=null;valueTooltip=null;valueTooltipPlacement=null;legend=null}class t1{active=!1;value=0;difference=0;position=0;lowLimit=0;highLimit=0}class cc{value;highValue;static compare(n,t){return!(I.isNullOrUndefined(n)&&I.isNullOrUndefined(t)||I.isNullOrUndefined(n)!==I.isNullOrUndefined(t))&&n.value===t.value&&n.highValue===t.highValue}}class n1 extends cc{forceChange;static compare(n,t){return!(I.isNullOrUndefined(n)&&I.isNullOrUndefined(t)||I.isNullOrUndefined(n)!==I.isNullOrUndefined(t))&&n.value===t.value&&n.highValue===t.highValue&&n.forceChange===t.forceChange}}const XB={provide:Zt,useExisting:fe(()=>o1),multi:!0};let o1=(()=>{class e{renderer;elementRef;changeDetectionRef;zone;allowUnsafeHtmlInSlider;sliderElementNgxSliderClass=!0;value=null;valueChange=new me;highValue=null;highValueChange=new me;options=new lc;userChangeStart=new me;userChange=new me;userChangeEnd=new me;manualRefreshSubscription;set manualRefresh(t){this.unsubscribeManualRefresh(),this.manualRefreshSubscription=t.subscribe(()=>{setTimeout(()=>this.calculateViewDimensionsAndDetectChanges())})}triggerFocusSubscription;set triggerFocus(t){this.unsubscribeTriggerFocus(),this.triggerFocusSubscription=t.subscribe(o=>{this.focusPointer(o)})}cancelUserChangeSubscription;set cancelUserChange(t){this.unsubscribeCancelUserChange(),this.cancelUserChangeSubscription=t.subscribe(()=>{this.moving&&(this.positionTrackingHandle(this.preStartHandleValue),this.forceEnd(!0))})}get range(){return!I.isNullOrUndefined(this.value)&&!I.isNullOrUndefined(this.highValue)}initHasRun=!1;inputModelChangeSubject=new Kt;inputModelChangeSubscription=null;outputModelChangeSubject=new Kt;outputModelChangeSubscription=null;viewLowValue=null;viewHighValue=null;viewOptions=new lc;handleHalfDimension=0;maxHandlePosition=0;currentTrackingPointer=null;currentFocusPointer=null;firstKeyDown=!1;touchId=null;dragging=new t1;preStartHandleValue=null;leftOuterSelectionBarElement;rightOuterSelectionBarElement;fullBarElement;selectionBarElement;minHandleElement;maxHandleElement;floorLabelElement;ceilLabelElement;minHandleLabelElement;maxHandleLabelElement;combinedLabelElement;ticksElement;tooltipTemplate;sliderElementVerticalClass=!1;sliderElementAnimateClass=!1;sliderElementWithLegendClass=!1;sliderElementDisabledAttr=null;sliderElementAriaLabel="ngx-slider";barStyle={};minPointerStyle={};maxPointerStyle={};fullBarTransparentClass=!1;selectionBarDraggableClass=!1;ticksUnderValuesClass=!1;get showTicks(){return this.viewOptions.showTicks}intermediateTicks=!1;ticks=[];eventListenerHelper=null;onMoveEventListener=null;onEndEventListener=null;moving=!1;resizeObserver=null;onTouchedCallback=null;onChangeCallback=null;constructor(t,o,i,r,s){this.renderer=t,this.elementRef=o,this.changeDetectionRef=i,this.zone=r,this.allowUnsafeHtmlInSlider=s,this.eventListenerHelper=new e1(this.renderer)}ngOnInit(){this.viewOptions=new lc,Object.assign(this.viewOptions,this.options),this.updateDisabledState(),this.updateVerticalState(),this.updateAriaLabel()}ngAfterViewInit(){this.applyOptions(),this.subscribeInputModelChangeSubject(),this.subscribeOutputModelChangeSubject(),this.renormaliseModelValues(),this.viewLowValue=this.modelValueToViewValue(this.value),this.viewHighValue=this.range?this.modelValueToViewValue(this.highValue):null,this.updateVerticalState(),this.manageElementsStyle(),this.updateDisabledState(),this.calculateViewDimensions(),this.addAccessibility(),this.updateCeilLabel(),this.updateFloorLabel(),this.initHandles(),this.manageEventsBindings(),this.updateAriaLabel(),this.subscribeResizeObserver(),this.initHasRun=!0,this.isRefDestroyed()||this.changeDetectionRef.detectChanges()}ngOnChanges(t){!I.isNullOrUndefined(t.options)&&JSON.stringify(t.options.previousValue)!==JSON.stringify(t.options.currentValue)&&this.onChangeOptions(),(!I.isNullOrUndefined(t.value)||!I.isNullOrUndefined(t.highValue))&&this.inputModelChangeSubject.next({value:this.value,highValue:this.highValue,controlAccessorChange:!1,forceChange:!1,internalChange:!1})}ngOnDestroy(){this.unbindEvents(),this.unsubscribeResizeObserver(),this.unsubscribeInputModelChangeSubject(),this.unsubscribeOutputModelChangeSubject(),this.unsubscribeManualRefresh(),this.unsubscribeTriggerFocus()}writeValue(t){t instanceof Array?(this.value=t[0],this.highValue=t[1]):this.value=t,this.inputModelChangeSubject.next({value:this.value,highValue:this.highValue,forceChange:!1,internalChange:!1,controlAccessorChange:!0})}registerOnChange(t){this.onChangeCallback=t}registerOnTouched(t){this.onTouchedCallback=t}setDisabledState(t){this.viewOptions.disabled=t,this.updateDisabledState(),this.initHasRun&&this.manageEventsBindings()}setAriaLabel(t){this.viewOptions.ariaLabel=t,this.updateAriaLabel()}onResize(t){this.calculateViewDimensionsAndDetectChanges()}subscribeInputModelChangeSubject(){this.inputModelChangeSubscription=this.inputModelChangeSubject.pipe(zE(n1.compare),function bB(e,n){return bo((t,o)=>{let i=0;t.subscribe(Pn(o,r=>e.call(n,r,i++)&&o.next(r)))})}(t=>!t.forceChange&&!t.internalChange)).subscribe(t=>this.applyInputModelChange(t))}subscribeOutputModelChangeSubject(){this.outputModelChangeSubscription=this.outputModelChangeSubject.pipe(zE(n1.compare)).subscribe(t=>this.publishOutputModelChange(t))}subscribeResizeObserver(){eo.isResizeObserverAvailable()&&(this.resizeObserver=new ResizeObserver(()=>this.calculateViewDimensionsAndDetectChanges()),this.resizeObserver.observe(this.elementRef.nativeElement))}unsubscribeResizeObserver(){eo.isResizeObserverAvailable()&&null!==this.resizeObserver&&(this.resizeObserver.disconnect(),this.resizeObserver=null)}unsubscribeOnMove(){I.isNullOrUndefined(this.onMoveEventListener)||(this.eventListenerHelper.detachEventListener(this.onMoveEventListener),this.onMoveEventListener=null)}unsubscribeOnEnd(){I.isNullOrUndefined(this.onEndEventListener)||(this.eventListenerHelper.detachEventListener(this.onEndEventListener),this.onEndEventListener=null)}unsubscribeInputModelChangeSubject(){I.isNullOrUndefined(this.inputModelChangeSubscription)||(this.inputModelChangeSubscription.unsubscribe(),this.inputModelChangeSubscription=null)}unsubscribeOutputModelChangeSubject(){I.isNullOrUndefined(this.outputModelChangeSubscription)||(this.outputModelChangeSubscription.unsubscribe(),this.outputModelChangeSubscription=null)}unsubscribeManualRefresh(){I.isNullOrUndefined(this.manualRefreshSubscription)||(this.manualRefreshSubscription.unsubscribe(),this.manualRefreshSubscription=null)}unsubscribeTriggerFocus(){I.isNullOrUndefined(this.triggerFocusSubscription)||(this.triggerFocusSubscription.unsubscribe(),this.triggerFocusSubscription=null)}unsubscribeCancelUserChange(){I.isNullOrUndefined(this.cancelUserChangeSubscription)||(this.cancelUserChangeSubscription.unsubscribe(),this.cancelUserChangeSubscription=null)}getPointerElement(t){return t===F.Min?this.minHandleElement:t===F.Max?this.maxHandleElement:null}getCurrentTrackingValue(){return this.currentTrackingPointer===F.Min?this.viewLowValue:this.currentTrackingPointer===F.Max?this.viewHighValue:null}modelValueToViewValue(t){return I.isNullOrUndefined(t)?NaN:I.isNullOrUndefined(this.viewOptions.stepsArray)||this.viewOptions.bindIndexForStepsArray?+t:I.findStepIndex(+t,this.viewOptions.stepsArray)}viewValueToModelValue(t){return I.isNullOrUndefined(this.viewOptions.stepsArray)||this.viewOptions.bindIndexForStepsArray?t:this.getStepValue(t)}getStepValue(t){const o=this.viewOptions.stepsArray[t];return I.isNullOrUndefined(o)?NaN:o.value}applyViewChange(){this.value=this.viewValueToModelValue(this.viewLowValue),this.range&&(this.highValue=this.viewValueToModelValue(this.viewHighValue)),this.outputModelChangeSubject.next({value:this.value,highValue:this.highValue,controlAccessorChange:!1,userEventInitiated:!0,forceChange:!1}),this.inputModelChangeSubject.next({value:this.value,highValue:this.highValue,controlAccessorChange:!1,forceChange:!1,internalChange:!0})}applyInputModelChange(t){const o=this.normaliseModelValues(t),i=!cc.compare(t,o);i&&(this.value=o.value,this.highValue=o.highValue),this.viewLowValue=this.modelValueToViewValue(o.value),this.viewHighValue=this.range?this.modelValueToViewValue(o.highValue):null,this.updateLowHandle(this.valueToPosition(this.viewLowValue)),this.range&&this.updateHighHandle(this.valueToPosition(this.viewHighValue)),this.updateSelectionBar(),this.updateTicksScale(),this.updateAriaAttributes(),this.range&&this.updateCombinedLabel(),this.outputModelChangeSubject.next({value:o.value,highValue:o.highValue,controlAccessorChange:t.controlAccessorChange,forceChange:i,userEventInitiated:!1})}publishOutputModelChange(t){const o=()=>{this.valueChange.emit(t.value),this.range&&this.highValueChange.emit(t.highValue),!t.controlAccessorChange&&(I.isNullOrUndefined(this.onChangeCallback)||this.onChangeCallback(this.range?[t.value,t.highValue]:t.value),I.isNullOrUndefined(this.onTouchedCallback)||this.onTouchedCallback(this.range?[t.value,t.highValue]:t.value))};t.userEventInitiated?(o(),this.userChange.emit(this.getChangeContext())):setTimeout(()=>{o()})}normaliseModelValues(t){const o=new cc;if(o.value=t.value,o.highValue=t.highValue,!I.isNullOrUndefined(this.viewOptions.stepsArray)){if(this.viewOptions.enforceStepsArray){const i=I.findStepIndex(o.value,this.viewOptions.stepsArray);if(o.value=this.viewOptions.stepsArray[i].value,this.range){const r=I.findStepIndex(o.highValue,this.viewOptions.stepsArray);o.highValue=this.viewOptions.stepsArray[r].value}}return o}if(this.viewOptions.enforceStep&&(o.value=this.roundStep(o.value),this.range&&(o.highValue=this.roundStep(o.highValue))),this.viewOptions.enforceRange&&(o.value=ke.clampToRange(o.value,this.viewOptions.floor,this.viewOptions.ceil),this.range&&(o.highValue=ke.clampToRange(o.highValue,this.viewOptions.floor,this.viewOptions.ceil)),this.range&&t.value>t.highValue))if(this.viewOptions.noSwitching)o.value=o.highValue;else{const i=t.value;o.value=t.highValue,o.highValue=i}return o}renormaliseModelValues(){const t={value:this.value,highValue:this.highValue},o=this.normaliseModelValues(t);cc.compare(o,t)||(this.value=o.value,this.highValue=o.highValue,this.outputModelChangeSubject.next({value:this.value,highValue:this.highValue,controlAccessorChange:!1,forceChange:!0,userEventInitiated:!1}))}onChangeOptions(){if(!this.initHasRun)return;const t=this.getOptionsInfluencingEventBindings(this.viewOptions);this.applyOptions();const o=this.getOptionsInfluencingEventBindings(this.viewOptions),i=!I.areArraysEqual(t,o);this.renormaliseModelValues(),this.viewLowValue=this.modelValueToViewValue(this.value),this.viewHighValue=this.range?this.modelValueToViewValue(this.highValue):null,this.resetSlider(i)}applyOptions(){if(this.viewOptions=new lc,Object.assign(this.viewOptions,this.options),this.viewOptions.draggableRange=this.range&&this.viewOptions.draggableRange,this.viewOptions.draggableRangeOnly=this.range&&this.viewOptions.draggableRangeOnly,this.viewOptions.draggableRangeOnly&&(this.viewOptions.draggableRange=!0),this.viewOptions.showTicks=this.viewOptions.showTicks||this.viewOptions.showTicksValues||!I.isNullOrUndefined(this.viewOptions.ticksArray),this.viewOptions.showTicks&&(!I.isNullOrUndefined(this.viewOptions.tickStep)||!I.isNullOrUndefined(this.viewOptions.ticksArray))&&(this.intermediateTicks=!0),this.viewOptions.showSelectionBar=this.viewOptions.showSelectionBar||this.viewOptions.showSelectionBarEnd||!I.isNullOrUndefined(this.viewOptions.showSelectionBarFromValue),I.isNullOrUndefined(this.viewOptions.stepsArray)?this.applyFloorCeilOptions():this.applyStepsArrayOptions(),I.isNullOrUndefined(this.viewOptions.combineLabels)&&(this.viewOptions.combineLabels=(t,o)=>t+" - "+o),this.viewOptions.logScale&&0===this.viewOptions.floor)throw Error("Can't use floor=0 with logarithmic scale")}applyStepsArrayOptions(){this.viewOptions.floor=0,this.viewOptions.ceil=this.viewOptions.stepsArray.length-1,this.viewOptions.step=1,I.isNullOrUndefined(this.viewOptions.translate)&&(this.viewOptions.translate=t=>String(this.viewOptions.bindIndexForStepsArray?this.getStepValue(t):t))}applyFloorCeilOptions(){if(I.isNullOrUndefined(this.viewOptions.step)?this.viewOptions.step=1:(this.viewOptions.step=+this.viewOptions.step,this.viewOptions.step<=0&&(this.viewOptions.step=1)),I.isNullOrUndefined(this.viewOptions.ceil)||I.isNullOrUndefined(this.viewOptions.floor))throw Error("floor and ceil options must be supplied");this.viewOptions.ceil=+this.viewOptions.ceil,this.viewOptions.floor=+this.viewOptions.floor,I.isNullOrUndefined(this.viewOptions.translate)&&(this.viewOptions.translate=t=>String(t))}resetSlider(t=!0){this.manageElementsStyle(),this.addAccessibility(),this.updateCeilLabel(),this.updateFloorLabel(),t&&(this.unbindEvents(),this.manageEventsBindings()),this.updateDisabledState(),this.updateAriaLabel(),this.calculateViewDimensions(),this.refocusPointerIfNeeded()}focusPointer(t){t!==F.Min&&t!==F.Max&&(t=F.Min),t===F.Min?this.minHandleElement.focus():this.range&&t===F.Max&&this.maxHandleElement.focus()}refocusPointerIfNeeded(){I.isNullOrUndefined(this.currentFocusPointer)||this.getPointerElement(this.currentFocusPointer).focusIfNeeded()}manageElementsStyle(){this.updateScale(),this.floorLabelElement.setAlwaysHide(this.viewOptions.showTicksValues||this.viewOptions.hideLimitLabels),this.ceilLabelElement.setAlwaysHide(this.viewOptions.showTicksValues||this.viewOptions.hideLimitLabels);const t=this.viewOptions.showTicksValues&&!this.intermediateTicks;this.minHandleLabelElement.setAlwaysHide(t||this.viewOptions.hidePointerLabels),this.maxHandleLabelElement.setAlwaysHide(t||!this.range||this.viewOptions.hidePointerLabels),this.combinedLabelElement.setAlwaysHide(t||!this.range||this.viewOptions.hidePointerLabels),this.selectionBarElement.setAlwaysHide(!this.range&&!this.viewOptions.showSelectionBar),this.leftOuterSelectionBarElement.setAlwaysHide(!this.range||!this.viewOptions.showOuterSelectionBars),this.rightOuterSelectionBarElement.setAlwaysHide(!this.range||!this.viewOptions.showOuterSelectionBars),this.fullBarTransparentClass=this.range&&this.viewOptions.showOuterSelectionBars,this.selectionBarDraggableClass=this.viewOptions.draggableRange&&!this.viewOptions.onlyBindHandles,this.ticksUnderValuesClass=this.intermediateTicks&&this.options.showTicksValues,this.sliderElementVerticalClass!==this.viewOptions.vertical&&(this.updateVerticalState(),setTimeout(()=>{this.resetSlider()})),this.sliderElementAnimateClass!==this.viewOptions.animate&&setTimeout(()=>{this.sliderElementAnimateClass=this.viewOptions.animate}),this.updateRotate()}manageEventsBindings(){this.viewOptions.disabled||this.viewOptions.readOnly?this.unbindEvents():this.bindEvents()}updateDisabledState(){this.sliderElementDisabledAttr=this.viewOptions.disabled?"disabled":null}updateAriaLabel(){this.sliderElementAriaLabel=this.viewOptions.ariaLabel||"nxg-slider"}updateVerticalState(){this.sliderElementVerticalClass=this.viewOptions.vertical;for(const t of this.getAllSliderElements())I.isNullOrUndefined(t)||t.setVertical(this.viewOptions.vertical)}updateScale(){for(const t of this.getAllSliderElements())t.setScale(this.viewOptions.scale)}updateRotate(){for(const t of this.getAllSliderElements())t.setRotate(this.viewOptions.rotate)}getAllSliderElements(){return[this.leftOuterSelectionBarElement,this.rightOuterSelectionBarElement,this.fullBarElement,this.selectionBarElement,this.minHandleElement,this.maxHandleElement,this.floorLabelElement,this.ceilLabelElement,this.minHandleLabelElement,this.maxHandleLabelElement,this.combinedLabelElement,this.ticksElement]}initHandles(){this.updateLowHandle(this.valueToPosition(this.viewLowValue)),this.range&&this.updateHighHandle(this.valueToPosition(this.viewHighValue)),this.updateSelectionBar(),this.range&&this.updateCombinedLabel(),this.updateTicksScale()}addAccessibility(){this.updateAriaAttributes(),this.minHandleElement.role="slider",this.minHandleElement.tabindex=!this.viewOptions.keyboardSupport||this.viewOptions.readOnly||this.viewOptions.disabled?"":"0",this.minHandleElement.ariaOrientation=this.viewOptions.vertical||0!==this.viewOptions.rotate?"vertical":"horizontal",I.isNullOrUndefined(this.viewOptions.ariaLabel)?I.isNullOrUndefined(this.viewOptions.ariaLabelledBy)||(this.minHandleElement.ariaLabelledBy=this.viewOptions.ariaLabelledBy):this.minHandleElement.ariaLabel=this.viewOptions.ariaLabel,this.range&&(this.maxHandleElement.role="slider",this.maxHandleElement.tabindex=!this.viewOptions.keyboardSupport||this.viewOptions.readOnly||this.viewOptions.disabled?"":"0",this.maxHandleElement.ariaOrientation=this.viewOptions.vertical||0!==this.viewOptions.rotate?"vertical":"horizontal",I.isNullOrUndefined(this.viewOptions.ariaLabelHigh)?I.isNullOrUndefined(this.viewOptions.ariaLabelledByHigh)||(this.maxHandleElement.ariaLabelledBy=this.viewOptions.ariaLabelledByHigh):this.maxHandleElement.ariaLabel=this.viewOptions.ariaLabelHigh)}updateAriaAttributes(){this.minHandleElement.ariaValueNow=(+this.value).toString(),this.minHandleElement.ariaValueText=this.viewOptions.translate(+this.value,cn.Low),this.minHandleElement.ariaValueMin=this.viewOptions.floor.toString(),this.minHandleElement.ariaValueMax=this.viewOptions.ceil.toString(),this.range&&(this.maxHandleElement.ariaValueNow=(+this.highValue).toString(),this.maxHandleElement.ariaValueText=this.viewOptions.translate(+this.highValue,cn.High),this.maxHandleElement.ariaValueMin=this.viewOptions.floor.toString(),this.maxHandleElement.ariaValueMax=this.viewOptions.ceil.toString())}calculateViewDimensions(){I.isNullOrUndefined(this.viewOptions.handleDimension)?this.minHandleElement.calculateDimension():this.minHandleElement.setDimension(this.viewOptions.handleDimension);const t=this.minHandleElement.dimension;this.handleHalfDimension=t/2,I.isNullOrUndefined(this.viewOptions.barDimension)?this.fullBarElement.calculateDimension():this.fullBarElement.setDimension(this.viewOptions.barDimension),this.maxHandlePosition=this.fullBarElement.dimension-t,this.initHasRun&&(this.updateFloorLabel(),this.updateCeilLabel(),this.initHandles())}calculateViewDimensionsAndDetectChanges(){this.calculateViewDimensions(),this.isRefDestroyed()||this.changeDetectionRef.detectChanges()}isRefDestroyed(){return this.changeDetectionRef.destroyed}updateTicksScale(){if(!this.viewOptions.showTicks&&this.sliderElementWithLegendClass)return void setTimeout(()=>{this.sliderElementWithLegendClass=!1});const t=I.isNullOrUndefined(this.viewOptions.ticksArray)?this.getTicksArray():this.viewOptions.ticksArray,o=this.viewOptions.vertical?"translateY":"translateX";this.viewOptions.rightToLeft&&t.reverse();const i=I.isNullOrUndefined(this.viewOptions.tickValueStep)?I.isNullOrUndefined(this.viewOptions.tickStep)?this.viewOptions.step:this.viewOptions.tickStep:this.viewOptions.tickValueStep;let r=!1;const s=t.map(a=>{let l=this.valueToPosition(a);this.viewOptions.vertical&&(l=this.maxHandlePosition-l);const c=o+"("+Math.round(l)+"px)",u=new KB;u.selected=this.isTickSelected(a),u.style={"-webkit-transform":c,"-moz-transform":c,"-o-transform":c,"-ms-transform":c,transform:c},u.selected&&!I.isNullOrUndefined(this.viewOptions.getSelectionBarColor)&&(u.style["background-color"]=this.getSelectionBarColor()),!u.selected&&!I.isNullOrUndefined(this.viewOptions.getTickColor)&&(u.style["background-color"]=this.getTickColor(a)),I.isNullOrUndefined(this.viewOptions.ticksTooltip)||(u.tooltip=this.viewOptions.ticksTooltip(a),u.tooltipPlacement=this.viewOptions.vertical?"right":"top"),this.viewOptions.showTicksValues&&!I.isNullOrUndefined(i)&&ke.isModuloWithinPrecisionLimit(a,i,this.viewOptions.precisionLimit)&&(u.value=this.getDisplayValue(a,cn.TickValue),I.isNullOrUndefined(this.viewOptions.ticksValuesTooltip)||(u.valueTooltip=this.viewOptions.ticksValuesTooltip(a),u.valueTooltipPlacement=this.viewOptions.vertical?"right":"top"));let d=null;if(I.isNullOrUndefined(this.viewOptions.stepsArray))I.isNullOrUndefined(this.viewOptions.getLegend)||(d=this.viewOptions.getLegend(a));else{const g=this.viewOptions.stepsArray[a];I.isNullOrUndefined(this.viewOptions.getStepLegend)?I.isNullOrUndefined(g)||(d=g.legend):d=this.viewOptions.getStepLegend(g)}return I.isNullOrUndefined(d)||(u.legend=d,r=!0),u});if(this.sliderElementWithLegendClass!==r&&setTimeout(()=>{this.sliderElementWithLegendClass=r}),I.isNullOrUndefined(this.ticks)||this.ticks.length!==s.length)this.ticks=s,this.isRefDestroyed()||this.changeDetectionRef.detectChanges();else for(let a=0;a<s.length;++a)Object.assign(this.ticks[a],s[a])}getTicksArray(){if(!this.viewOptions.showTicks)return[];const t=I.isNullOrUndefined(this.viewOptions.tickStep)?this.viewOptions.step:this.viewOptions.tickStep,o=[],i=1+Math.floor(ke.roundToPrecisionLimit(Math.abs(this.viewOptions.ceil-this.viewOptions.floor)/t,this.viewOptions.precisionLimit));for(let r=0;r<i;++r)o.push(ke.roundToPrecisionLimit(this.viewOptions.floor+t*r,this.viewOptions.precisionLimit));return o}isTickSelected(t){if(!this.range)if(I.isNullOrUndefined(this.viewOptions.showSelectionBarFromValue)){if(this.viewOptions.showSelectionBarEnd){if(t>=this.viewLowValue)return!0}else if(this.viewOptions.showSelectionBar&&t<=this.viewLowValue)return!0}else{const o=this.viewOptions.showSelectionBarFromValue;if(this.viewLowValue>o&&t>=o&&t<=this.viewLowValue)return!0;if(this.viewLowValue<o&&t<=o&&t>=this.viewLowValue)return!0}return!!(this.range&&t>=this.viewLowValue&&t<=this.viewHighValue)}updateFloorLabel(){this.floorLabelElement.alwaysHide||(this.floorLabelElement.setValue(this.getDisplayValue(this.viewOptions.floor,cn.Floor)),this.floorLabelElement.calculateDimension(),this.floorLabelElement.setPosition(this.viewOptions.rightToLeft?this.fullBarElement.dimension-this.floorLabelElement.dimension:0))}updateCeilLabel(){this.ceilLabelElement.alwaysHide||(this.ceilLabelElement.setValue(this.getDisplayValue(this.viewOptions.ceil,cn.Ceil)),this.ceilLabelElement.calculateDimension(),this.ceilLabelElement.setPosition(this.viewOptions.rightToLeft?0:this.fullBarElement.dimension-this.ceilLabelElement.dimension))}updateHandles(t,o){t===F.Min?this.updateLowHandle(o):t===F.Max&&this.updateHighHandle(o),this.updateSelectionBar(),this.updateTicksScale(),this.range&&this.updateCombinedLabel()}getHandleLabelPos(t,o){const i=t===F.Min?this.minHandleLabelElement.dimension:this.maxHandleLabelElement.dimension,r=o-i/2+this.handleHalfDimension,s=this.fullBarElement.dimension-i;return this.viewOptions.boundPointerLabels?this.viewOptions.rightToLeft&&t===F.Min||!this.viewOptions.rightToLeft&&t===F.Max?Math.min(r,s):Math.min(Math.max(r,0),s):r}updateLowHandle(t){this.minHandleElement.setPosition(t),this.minHandleLabelElement.setValue(this.getDisplayValue(this.viewLowValue,cn.Low)),this.minHandleLabelElement.setPosition(this.getHandleLabelPos(F.Min,t)),I.isNullOrUndefined(this.viewOptions.getPointerColor)||(this.minPointerStyle={backgroundColor:this.getPointerColor(F.Min)}),this.viewOptions.autoHideLimitLabels&&this.updateFloorAndCeilLabelsVisibility()}updateHighHandle(t){this.maxHandleElement.setPosition(t),this.maxHandleLabelElement.setValue(this.getDisplayValue(this.viewHighValue,cn.High)),this.maxHandleLabelElement.setPosition(this.getHandleLabelPos(F.Max,t)),I.isNullOrUndefined(this.viewOptions.getPointerColor)||(this.maxPointerStyle={backgroundColor:this.getPointerColor(F.Max)}),this.viewOptions.autoHideLimitLabels&&this.updateFloorAndCeilLabelsVisibility()}updateFloorAndCeilLabelsVisibility(){if(this.viewOptions.hidePointerLabels)return;let t=!1,o=!1;const i=this.isLabelBelowFloorLabel(this.minHandleLabelElement),r=this.isLabelAboveCeilLabel(this.minHandleLabelElement),s=this.isLabelAboveCeilLabel(this.maxHandleLabelElement),a=this.isLabelBelowFloorLabel(this.combinedLabelElement),l=this.isLabelAboveCeilLabel(this.combinedLabelElement);if(i?(t=!0,this.floorLabelElement.hide()):(t=!1,this.floorLabelElement.show()),r?(o=!0,this.ceilLabelElement.hide()):(o=!1,this.ceilLabelElement.show()),this.range){const c=this.combinedLabelElement.isVisible()?l:s,u=this.combinedLabelElement.isVisible()?a:i;c?this.ceilLabelElement.hide():o||this.ceilLabelElement.show(),u?this.floorLabelElement.hide():t||this.floorLabelElement.show()}}isLabelBelowFloorLabel(t){const o=t.position,r=this.floorLabelElement.position;return this.viewOptions.rightToLeft?o+t.dimension>=r-2:o<=r+this.floorLabelElement.dimension+2}isLabelAboveCeilLabel(t){const o=t.position,r=this.ceilLabelElement.position;return this.viewOptions.rightToLeft?o<=r+this.ceilLabelElement.dimension+2:o+t.dimension>=r-2}updateSelectionBar(){let t=0,o=0;const i=this.viewOptions.rightToLeft?!this.viewOptions.showSelectionBarEnd:this.viewOptions.showSelectionBarEnd,r=this.viewOptions.rightToLeft?this.maxHandleElement.position+this.handleHalfDimension:this.minHandleElement.position+this.handleHalfDimension;if(this.range)o=Math.abs(this.maxHandleElement.position-this.minHandleElement.position),t=r;else if(I.isNullOrUndefined(this.viewOptions.showSelectionBarFromValue))i?(o=Math.ceil(Math.abs(this.maxHandlePosition-this.minHandleElement.position)+this.handleHalfDimension),t=Math.floor(this.minHandleElement.position+this.handleHalfDimension)):(o=this.minHandleElement.position+this.handleHalfDimension,t=0);else{const s=this.viewOptions.showSelectionBarFromValue,a=this.valueToPosition(s);(this.viewOptions.rightToLeft?this.viewLowValue<=s:this.viewLowValue>s)?(o=this.minHandleElement.position-a,t=a+this.handleHalfDimension):(o=a-this.minHandleElement.position,t=this.minHandleElement.position+this.handleHalfDimension)}if(this.selectionBarElement.setDimension(o),this.selectionBarElement.setPosition(t),this.range&&this.viewOptions.showOuterSelectionBars&&(this.viewOptions.rightToLeft?(this.rightOuterSelectionBarElement.setDimension(t),this.rightOuterSelectionBarElement.setPosition(0),this.fullBarElement.calculateDimension(),this.leftOuterSelectionBarElement.setDimension(this.fullBarElement.dimension-(t+o)),this.leftOuterSelectionBarElement.setPosition(t+o)):(this.leftOuterSelectionBarElement.setDimension(t),this.leftOuterSelectionBarElement.setPosition(0),this.fullBarElement.calculateDimension(),this.rightOuterSelectionBarElement.setDimension(this.fullBarElement.dimension-(t+o)),this.rightOuterSelectionBarElement.setPosition(t+o))),I.isNullOrUndefined(this.viewOptions.getSelectionBarColor)){if(!I.isNullOrUndefined(this.viewOptions.selectionBarGradient)){const s=I.isNullOrUndefined(this.viewOptions.showSelectionBarFromValue)?0:this.valueToPosition(this.viewOptions.showSelectionBarFromValue),a=s-t>0&&!i||s-t<=0&&i;this.barStyle={backgroundImage:"linear-gradient(to "+(this.viewOptions.vertical?a?"bottom":"top":a?"left":"right")+", "+this.viewOptions.selectionBarGradient.from+" 0%,"+this.viewOptions.selectionBarGradient.to+" 100%)"},this.viewOptions.vertical?(this.barStyle.backgroundPosition="center "+(s+o+t+(a?-this.handleHalfDimension:0))+"px",this.barStyle.backgroundSize="100% "+(this.fullBarElement.dimension-this.handleHalfDimension)+"px"):(this.barStyle.backgroundPosition=s-t+(a?this.handleHalfDimension:0)+"px center",this.barStyle.backgroundSize=this.fullBarElement.dimension-this.handleHalfDimension+"px 100%")}}else{const s=this.getSelectionBarColor();this.barStyle={backgroundColor:s}}}getSelectionBarColor(){return this.range?this.viewOptions.getSelectionBarColor(this.value,this.highValue):this.viewOptions.getSelectionBarColor(this.value)}getPointerColor(t){return this.viewOptions.getPointerColor(t===F.Max?this.highValue:this.value,t)}getTickColor(t){return this.viewOptions.getTickColor(t)}updateCombinedLabel(){let t=null;if(t=this.viewOptions.rightToLeft?this.minHandleLabelElement.position-this.minHandleLabelElement.dimension-10<=this.maxHandleLabelElement.position:this.minHandleLabelElement.position+this.minHandleLabelElement.dimension+10>=this.maxHandleLabelElement.position,t){const o=this.getDisplayValue(this.viewLowValue,cn.Low),i=this.getDisplayValue(this.viewHighValue,cn.High),r=this.viewOptions.rightToLeft?this.viewOptions.combineLabels(i,o):this.viewOptions.combineLabels(o,i);this.combinedLabelElement.setValue(r);const s=this.viewOptions.boundPointerLabels?Math.min(Math.max(this.selectionBarElement.position+this.selectionBarElement.dimension/2-this.combinedLabelElement.dimension/2,0),this.fullBarElement.dimension-this.combinedLabelElement.dimension):this.selectionBarElement.position+this.selectionBarElement.dimension/2-this.combinedLabelElement.dimension/2;this.combinedLabelElement.setPosition(s),this.minHandleLabelElement.hide(),this.maxHandleLabelElement.hide(),this.combinedLabelElement.show()}else this.updateHighHandle(this.valueToPosition(this.viewHighValue)),this.updateLowHandle(this.valueToPosition(this.viewLowValue)),this.maxHandleLabelElement.show(),this.minHandleLabelElement.show(),this.combinedLabelElement.hide();this.viewOptions.autoHideLimitLabels&&this.updateFloorAndCeilLabelsVisibility()}getDisplayValue(t,o){return!I.isNullOrUndefined(this.viewOptions.stepsArray)&&!this.viewOptions.bindIndexForStepsArray&&(t=this.getStepValue(t)),this.viewOptions.translate(t,o)}roundStep(t,o){const i=I.isNullOrUndefined(o)?this.viewOptions.step:o;let r=ke.roundToPrecisionLimit((t-this.viewOptions.floor)/i,this.viewOptions.precisionLimit);return r=Math.round(r)*i,ke.roundToPrecisionLimit(this.viewOptions.floor+r,this.viewOptions.precisionLimit)}valueToPosition(t){let o=I.linearValueToPosition;I.isNullOrUndefined(this.viewOptions.customValueToPosition)?this.viewOptions.logScale&&(o=I.logValueToPosition):o=this.viewOptions.customValueToPosition;let i=o(t=ke.clampToRange(t,this.viewOptions.floor,this.viewOptions.ceil),this.viewOptions.floor,this.viewOptions.ceil);return I.isNullOrUndefined(i)&&(i=0),this.viewOptions.rightToLeft&&(i=1-i),i*this.maxHandlePosition}positionToValue(t){let o=t/this.maxHandlePosition;this.viewOptions.rightToLeft&&(o=1-o);let i=I.linearPositionToValue;I.isNullOrUndefined(this.viewOptions.customPositionToValue)?this.viewOptions.logScale&&(i=I.logPositionToValue):i=this.viewOptions.customPositionToValue;const r=i(o,this.viewOptions.floor,this.viewOptions.ceil);return I.isNullOrUndefined(r)?0:r}getEventXY(t,o){if(t instanceof MouseEvent)return this.viewOptions.vertical||0!==this.viewOptions.rotate?t.clientY:t.clientX;let i=0;const r=t.touches;if(!I.isNullOrUndefined(o))for(let s=0;s<r.length;s++)if(r[s].identifier===o){i=s;break}return this.viewOptions.vertical||0!==this.viewOptions.rotate?r[i].clientY:r[i].clientX}getEventPosition(t,o){const i=this.elementRef.nativeElement.getBoundingClientRect(),r=this.viewOptions.vertical||0!==this.viewOptions.rotate?i.bottom:i.left;let s=0;return s=this.viewOptions.vertical||0!==this.viewOptions.rotate?-this.getEventXY(t,o)+r:this.getEventXY(t,o)-r,s*this.viewOptions.scale-this.handleHalfDimension}getNearestHandle(t){if(!this.range)return F.Min;const o=this.getEventPosition(t),i=Math.abs(o-this.minHandleElement.position),r=Math.abs(o-this.maxHandleElement.position);return i<r?F.Min:i>r?F.Max:this.viewOptions.rightToLeft?o>this.minHandleElement.position?F.Min:F.Max:o<this.minHandleElement.position?F.Min:F.Max}bindEvents(){const t=this.viewOptions.draggableRange;this.viewOptions.onlyBindHandles||this.selectionBarElement.on("mousedown",o=>this.onBarStart(null,t,o,!0,!0,!0)),this.viewOptions.draggableRangeOnly?(this.minHandleElement.on("mousedown",o=>this.onBarStart(F.Min,t,o,!0,!0)),this.maxHandleElement.on("mousedown",o=>this.onBarStart(F.Max,t,o,!0,!0))):(this.minHandleElement.on("mousedown",o=>this.onStart(F.Min,o,!0,!0)),this.range&&this.maxHandleElement.on("mousedown",o=>this.onStart(F.Max,o,!0,!0)),this.viewOptions.onlyBindHandles||(this.fullBarElement.on("mousedown",o=>this.onStart(null,o,!0,!0,!0)),this.ticksElement.on("mousedown",o=>this.onStart(null,o,!0,!0,!0,!0)))),this.viewOptions.onlyBindHandles||this.selectionBarElement.onPassive("touchstart",o=>this.onBarStart(null,t,o,!0,!0,!0)),this.viewOptions.draggableRangeOnly?(this.minHandleElement.onPassive("touchstart",o=>this.onBarStart(F.Min,t,o,!0,!0)),this.maxHandleElement.onPassive("touchstart",o=>this.onBarStart(F.Max,t,o,!0,!0))):(this.minHandleElement.onPassive("touchstart",o=>this.onStart(F.Min,o,!0,!0)),this.range&&this.maxHandleElement.onPassive("touchstart",o=>this.onStart(F.Max,o,!0,!0)),this.viewOptions.onlyBindHandles||(this.fullBarElement.onPassive("touchstart",o=>this.onStart(null,o,!0,!0,!0)),this.ticksElement.onPassive("touchstart",o=>this.onStart(null,o,!1,!1,!0,!0)))),this.viewOptions.keyboardSupport&&(this.minHandleElement.on("focus",()=>this.onPointerFocus(F.Min)),this.range&&this.maxHandleElement.on("focus",()=>this.onPointerFocus(F.Max)))}getOptionsInfluencingEventBindings(t){return[t.disabled,t.readOnly,t.draggableRange,t.draggableRangeOnly,t.onlyBindHandles,t.keyboardSupport]}unbindEvents(){this.unsubscribeOnMove(),this.unsubscribeOnEnd();for(const t of this.getAllSliderElements())I.isNullOrUndefined(t)||t.off()}onBarStart(t,o,i,r,s,a,l){o?this.onDragStart(t,i,r,s):this.onStart(t,i,r,s,a,l)}onStart(t,o,i,r,s,a){o.stopPropagation(),!eo.isTouchEvent(o)&&!WE&&o.preventDefault(),this.moving=!1,this.calculateViewDimensions(),I.isNullOrUndefined(t)&&(t=this.getNearestHandle(o)),this.currentTrackingPointer=t;const l=this.getPointerElement(t);if(l.active=!0,this.preStartHandleValue=this.getCurrentTrackingValue(),this.viewOptions.keyboardSupport&&l.focus(),i){this.unsubscribeOnMove();const c=u=>this.dragging.active?this.onDragMove(u):this.onMove(u);this.onMoveEventListener=eo.isTouchEvent(o)?this.eventListenerHelper.attachPassiveEventListener(document,"touchmove",c):this.eventListenerHelper.attachEventListener(document,"mousemove",c)}if(r){this.unsubscribeOnEnd();const c=u=>this.onEnd(u);this.onEndEventListener=eo.isTouchEvent(o)?this.eventListenerHelper.attachPassiveEventListener(document,"touchend",c):this.eventListenerHelper.attachEventListener(document,"mouseup",c)}this.userChangeStart.emit(this.getChangeContext()),eo.isTouchEvent(o)&&!I.isNullOrUndefined(o.changedTouches)&&I.isNullOrUndefined(this.touchId)&&(this.touchId=o.changedTouches[0].identifier),s&&this.onMove(o,!0),a&&this.onEnd(o)}onMove(t,o){let i=null;if(eo.isTouchEvent(t)){const c=t.changedTouches;for(let u=0;u<c.length;u++)if(c[u].identifier===this.touchId){i=c[u];break}if(I.isNullOrUndefined(i))return}this.viewOptions.animate&&!this.viewOptions.animateOnMove&&this.moving&&(this.sliderElementAnimateClass=!1),this.moving=!0;const r=I.isNullOrUndefined(i)?this.getEventPosition(t):this.getEventPosition(t,i.identifier);let s;r<=0?s=this.viewOptions.rightToLeft?this.viewOptions.ceil:this.viewOptions.floor:r>=this.maxHandlePosition?s=this.viewOptions.rightToLeft?this.viewOptions.floor:this.viewOptions.ceil:(s=this.positionToValue(r),s=o&&!I.isNullOrUndefined(this.viewOptions.tickStep)?this.roundStep(s,this.viewOptions.tickStep):this.roundStep(s)),this.positionTrackingHandle(s)}forceEnd(t=!1){this.moving=!1,this.viewOptions.animate&&(this.sliderElementAnimateClass=!0),t&&(this.sliderElementAnimateClass=!1,setTimeout(()=>{this.sliderElementAnimateClass=this.viewOptions.animate})),this.touchId=null,this.viewOptions.keyboardSupport||(this.minHandleElement.active=!1,this.maxHandleElement.active=!1,this.currentTrackingPointer=null),this.dragging.active=!1,this.unsubscribeOnMove(),this.unsubscribeOnEnd(),this.userChangeEnd.emit(this.getChangeContext())}onEnd(t){eo.isTouchEvent(t)&&t.changedTouches[0].identifier!==this.touchId||this.forceEnd()}onPointerFocus(t){const o=this.getPointerElement(t);o.on("blur",()=>this.onPointerBlur(o)),o.on("keydown",i=>this.onKeyboardEvent(i)),o.on("keyup",()=>this.onKeyUp()),o.active=!0,this.currentTrackingPointer=t,this.currentFocusPointer=t,this.firstKeyDown=!0}onKeyUp(){this.firstKeyDown=!0,this.userChangeEnd.emit(this.getChangeContext())}onPointerBlur(t){t.off("blur"),t.off("keydown"),t.off("keyup"),t.active=!1,I.isNullOrUndefined(this.touchId)&&(this.currentTrackingPointer=null,this.currentFocusPointer=null)}getKeyActions(t){const o=this.viewOptions.ceil-this.viewOptions.floor;let i=t+this.viewOptions.step,r=t-this.viewOptions.step,s=t+o/10,a=t-o/10;this.viewOptions.reversedControls&&(i=t-this.viewOptions.step,r=t+this.viewOptions.step,s=t-o/10,a=t+o/10);const l={UP:i,DOWN:r,LEFT:r,RIGHT:i,PAGEUP:s,PAGEDOWN:a,HOME:this.viewOptions.reversedControls?this.viewOptions.ceil:this.viewOptions.floor,END:this.viewOptions.reversedControls?this.viewOptions.floor:this.viewOptions.ceil};return this.viewOptions.rightToLeft&&(l.LEFT=i,l.RIGHT=r,(this.viewOptions.vertical||0!==this.viewOptions.rotate)&&(l.UP=r,l.DOWN=i)),l}onKeyboardEvent(t){const o=this.getCurrentTrackingValue(),i=I.isNullOrUndefined(t.keyCode)?t.which:t.keyCode,l=this.getKeyActions(o)[{38:"UP",40:"DOWN",37:"LEFT",39:"RIGHT",33:"PAGEUP",34:"PAGEDOWN",36:"HOME",35:"END"}[i]];if(I.isNullOrUndefined(l)||I.isNullOrUndefined(this.currentTrackingPointer))return;t.preventDefault(),this.firstKeyDown&&(this.firstKeyDown=!1,this.userChangeStart.emit(this.getChangeContext()));const c=ke.clampToRange(l,this.viewOptions.floor,this.viewOptions.ceil),u=this.roundStep(c);if(this.viewOptions.draggableRangeOnly){const d=this.viewHighValue-this.viewLowValue;let g,h;this.currentTrackingPointer===F.Min?(g=u,h=u+d,h>this.viewOptions.ceil&&(h=this.viewOptions.ceil,g=h-d)):this.currentTrackingPointer===F.Max&&(h=u,g=u-d,g<this.viewOptions.floor&&(g=this.viewOptions.floor,h=g+d)),this.positionTrackingBar(g,h)}else this.positionTrackingHandle(u)}onDragStart(t,o,i,r){const s=this.getEventPosition(o);this.dragging=new t1,this.dragging.active=!0,this.dragging.value=this.positionToValue(s),this.dragging.difference=this.viewHighValue-this.viewLowValue,this.dragging.lowLimit=this.viewOptions.rightToLeft?this.minHandleElement.position-s:s-this.minHandleElement.position,this.dragging.highLimit=this.viewOptions.rightToLeft?s-this.maxHandleElement.position:this.maxHandleElement.position-s,this.onStart(t,o,i,r)}getMinValue(t,o,i){const r=this.viewOptions.rightToLeft;let s=null;return s=o?i?r?this.viewOptions.floor:this.viewOptions.ceil-this.dragging.difference:r?this.viewOptions.ceil-this.dragging.difference:this.viewOptions.floor:this.positionToValue(r?t+this.dragging.lowLimit:t-this.dragging.lowLimit),this.roundStep(s)}getMaxValue(t,o,i){const r=this.viewOptions.rightToLeft;let s=null;return s=o?i?r?this.viewOptions.floor+this.dragging.difference:this.viewOptions.ceil:r?this.viewOptions.ceil:this.viewOptions.floor+this.dragging.difference:r?this.positionToValue(t+this.dragging.lowLimit)+this.dragging.difference:this.positionToValue(t-this.dragging.lowLimit)+this.dragging.difference,this.roundStep(s)}onDragMove(t){const o=this.getEventPosition(t);let i,r,s,a;this.viewOptions.animate&&!this.viewOptions.animateOnMove&&this.moving&&(this.sliderElementAnimateClass=!1),this.moving=!0,this.viewOptions.rightToLeft?(i=this.dragging.lowLimit,r=this.dragging.highLimit,s=this.maxHandleElement,a=this.minHandleElement):(i=this.dragging.highLimit,r=this.dragging.lowLimit,s=this.minHandleElement,a=this.maxHandleElement);const c=o>=this.maxHandlePosition-i;let u,d;if(o<=r){if(0===s.position)return;u=this.getMinValue(o,!0,!1),d=this.getMaxValue(o,!0,!1)}else if(c){if(a.position===this.maxHandlePosition)return;d=this.getMaxValue(o,!0,!0),u=this.getMinValue(o,!0,!0)}else u=this.getMinValue(o,!1,!1),d=this.getMaxValue(o,!1,!1);this.positionTrackingBar(u,d)}positionTrackingBar(t,o){!I.isNullOrUndefined(this.viewOptions.minLimit)&&t<this.viewOptions.minLimit&&(o=ke.roundToPrecisionLimit((t=this.viewOptions.minLimit)+this.dragging.difference,this.viewOptions.precisionLimit)),!I.isNullOrUndefined(this.viewOptions.maxLimit)&&o>this.viewOptions.maxLimit&&(t=ke.roundToPrecisionLimit((o=this.viewOptions.maxLimit)-this.dragging.difference,this.viewOptions.precisionLimit)),this.viewLowValue=t,this.viewHighValue=o,this.applyViewChange(),this.updateHandles(F.Min,this.valueToPosition(t)),this.updateHandles(F.Max,this.valueToPosition(o))}positionTrackingHandle(t){t=this.applyMinMaxLimit(t),this.range&&(this.viewOptions.pushRange?t=this.applyPushRange(t):(this.viewOptions.noSwitching&&(this.currentTrackingPointer===F.Min&&t>this.viewHighValue?t=this.applyMinMaxRange(this.viewHighValue):this.currentTrackingPointer===F.Max&&t<this.viewLowValue&&(t=this.applyMinMaxRange(this.viewLowValue))),t=this.applyMinMaxRange(t),this.currentTrackingPointer===F.Min&&t>this.viewHighValue?(this.viewLowValue=this.viewHighValue,this.applyViewChange(),this.updateHandles(F.Min,this.maxHandleElement.position),this.updateAriaAttributes(),this.currentTrackingPointer=F.Max,this.minHandleElement.active=!1,this.maxHandleElement.active=!0,this.viewOptions.keyboardSupport&&this.maxHandleElement.focus()):this.currentTrackingPointer===F.Max&&t<this.viewLowValue&&(this.viewHighValue=this.viewLowValue,this.applyViewChange(),this.updateHandles(F.Max,this.minHandleElement.position),this.updateAriaAttributes(),this.currentTrackingPointer=F.Min,this.maxHandleElement.active=!1,this.minHandleElement.active=!0,this.viewOptions.keyboardSupport&&this.minHandleElement.focus()))),this.getCurrentTrackingValue()!==t&&(this.currentTrackingPointer===F.Min?(this.viewLowValue=t,this.applyViewChange()):this.currentTrackingPointer===F.Max&&(this.viewHighValue=t,this.applyViewChange()),this.updateHandles(this.currentTrackingPointer,this.valueToPosition(t)),this.updateAriaAttributes())}applyMinMaxLimit(t){return!I.isNullOrUndefined(this.viewOptions.minLimit)&&t<this.viewOptions.minLimit?this.viewOptions.minLimit:!I.isNullOrUndefined(this.viewOptions.maxLimit)&&t>this.viewOptions.maxLimit?this.viewOptions.maxLimit:t}applyMinMaxRange(t){const i=Math.abs(t-(this.currentTrackingPointer===F.Min?this.viewHighValue:this.viewLowValue));if(!I.isNullOrUndefined(this.viewOptions.minRange)&&i<this.viewOptions.minRange){if(this.currentTrackingPointer===F.Min)return ke.roundToPrecisionLimit(this.viewHighValue-this.viewOptions.minRange,this.viewOptions.precisionLimit);if(this.currentTrackingPointer===F.Max)return ke.roundToPrecisionLimit(this.viewLowValue+this.viewOptions.minRange,this.viewOptions.precisionLimit)}if(!I.isNullOrUndefined(this.viewOptions.maxRange)&&i>this.viewOptions.maxRange){if(this.currentTrackingPointer===F.Min)return ke.roundToPrecisionLimit(this.viewHighValue-this.viewOptions.maxRange,this.viewOptions.precisionLimit);if(this.currentTrackingPointer===F.Max)return ke.roundToPrecisionLimit(this.viewLowValue+this.viewOptions.maxRange,this.viewOptions.precisionLimit)}return t}applyPushRange(t){const o=this.currentTrackingPointer===F.Min?this.viewHighValue-t:t-this.viewLowValue,i=I.isNullOrUndefined(this.viewOptions.minRange)?this.viewOptions.step:this.viewOptions.minRange,r=this.viewOptions.maxRange;return o<i?(this.currentTrackingPointer===F.Min?(this.viewHighValue=ke.roundToPrecisionLimit(Math.min(t+i,this.viewOptions.ceil),this.viewOptions.precisionLimit),t=ke.roundToPrecisionLimit(this.viewHighValue-i,this.viewOptions.precisionLimit),this.applyViewChange(),this.updateHandles(F.Max,this.valueToPosition(this.viewHighValue))):this.currentTrackingPointer===F.Max&&(this.viewLowValue=ke.roundToPrecisionLimit(Math.max(t-i,this.viewOptions.floor),this.viewOptions.precisionLimit),t=ke.roundToPrecisionLimit(this.viewLowValue+i,this.viewOptions.precisionLimit),this.applyViewChange(),this.updateHandles(F.Min,this.valueToPosition(this.viewLowValue))),this.updateAriaAttributes()):!I.isNullOrUndefined(r)&&o>r&&(this.currentTrackingPointer===F.Min?(this.viewHighValue=ke.roundToPrecisionLimit(t+r,this.viewOptions.precisionLimit),this.applyViewChange(),this.updateHandles(F.Max,this.valueToPosition(this.viewHighValue))):this.currentTrackingPointer===F.Max&&(this.viewLowValue=ke.roundToPrecisionLimit(t-r,this.viewOptions.precisionLimit),this.applyViewChange(),this.updateHandles(F.Min,this.valueToPosition(this.viewLowValue))),this.updateAriaAttributes()),t}getChangeContext(){const t=new ZB;return t.pointerType=this.currentTrackingPointer,t.value=+this.value,this.range&&(t.highValue=+this.highValue),t}static \u0275fac=function(o){return new(o||e)(T(qt),T(at),T(zi),T(ae),T(KE,8))};static \u0275cmp=Wt({type:e,selectors:[["ngx-slider"]],contentQueries:function(o,i,r){if(1&o&&NC(r,xB,5),2&o){let s;Dt(s=wt())&&(i.tooltipTemplate=s.first)}},viewQuery:function(o,i){if(1&o&&(Ot(kB,5,to),Ot(RB,5,to),Ot(FB,5,to),Ot(LB,5,to),Ot(PB,5,rg),Ot(VB,5,rg),Ot(HB,5,Qi),Ot(BB,5,Qi),Ot(jB,5,Qi),Ot(UB,5,Qi),Ot($B,5,Qi),Ot(zB,5,to)),2&o){let r;Dt(r=wt())&&(i.leftOuterSelectionBarElement=r.first),Dt(r=wt())&&(i.rightOuterSelectionBarElement=r.first),Dt(r=wt())&&(i.fullBarElement=r.first),Dt(r=wt())&&(i.selectionBarElement=r.first),Dt(r=wt())&&(i.minHandleElement=r.first),Dt(r=wt())&&(i.maxHandleElement=r.first),Dt(r=wt())&&(i.floorLabelElement=r.first),Dt(r=wt())&&(i.ceilLabelElement=r.first),Dt(r=wt())&&(i.minHandleLabelElement=r.first),Dt(r=wt())&&(i.maxHandleLabelElement=r.first),Dt(r=wt())&&(i.combinedLabelElement=r.first),Dt(r=wt())&&(i.ticksElement=r.first)}},hostVars:10,hostBindings:function(o,i){1&o&&j("resize",function(s){return i.onResize(s)},Oa),2&o&&(lt("disabled",i.sliderElementDisabledAttr)("aria-label",i.sliderElementAriaLabel),En("ngx-slider",i.sliderElementNgxSliderClass)("vertical",i.sliderElementVerticalClass)("animate",i.sliderElementAnimateClass)("with-legend",i.sliderElementWithLegendClass))},inputs:{value:"value",highValue:"highValue",options:"options",manualRefresh:"manualRefresh",triggerFocus:"triggerFocus",cancelUserChange:"cancelUserChange"},outputs:{valueChange:"valueChange",highValueChange:"highValueChange",userChangeStart:"userChangeStart",userChange:"userChange",userChangeEnd:"userChangeEnd"},standalone:!1,features:[be([XB]),Cn],decls:29,vars:13,consts:[["leftOuterSelectionBar",""],["rightOuterSelectionBar",""],["fullBar",""],["selectionBar",""],["minHandle",""],["maxHandle",""],["floorLabel",""],["ceilLabel",""],["minHandleLabel",""],["maxHandleLabel",""],["combinedLabel",""],["ticksElement",""],["ngxSliderElement","",1,"ngx-slider-span","ngx-slider-bar-wrapper","ngx-slider-left-out-selection"],[1,"ngx-slider-span","ngx-slider-bar"],["ngxSliderElement","",1,"ngx-slider-span","ngx-slider-bar-wrapper","ngx-slider-right-out-selection"],["ngxSliderElement","",1,"ngx-slider-span","ngx-slider-bar-wrapper","ngx-slider-full-bar"],["ngxSliderElement","",1,"ngx-slider-span","ngx-slider-bar-wrapper","ngx-slider-selection-bar"],[1,"ngx-slider-span","ngx-slider-bar","ngx-slider-selection",3,"ngStyle"],["ngxSliderHandle","",1,"ngx-slider-span","ngx-slider-pointer","ngx-slider-pointer-min",3,"ngStyle"],["ngxSliderHandle","",1,"ngx-slider-span","ngx-slider-pointer","ngx-slider-pointer-max",3,"ngStyle"],["ngxSliderLabel","",1,"ngx-slider-span","ngx-slider-bubble","ngx-slider-limit","ngx-slider-floor"],["ngxSliderLabel","",1,"ngx-slider-span","ngx-slider-bubble","ngx-slider-limit","ngx-slider-ceil"],["ngxSliderLabel","",1,"ngx-slider-span","ngx-slider-bubble","ngx-slider-model-value"],["ngxSliderLabel","",1,"ngx-slider-span","ngx-slider-bubble","ngx-slider-model-high"],["ngxSliderLabel","",1,"ngx-slider-span","ngx-slider-bubble","ngx-slider-combined"],["ngxSliderElement","",1,"ngx-slider-ticks",3,"hidden"],["class","ngx-slider-tick",3,"ngClass","ngStyle",4,"ngFor","ngForOf"],[1,"ngx-slider-tick",3,"ngClass","ngStyle"],[3,"template","tooltip","placement"],["class","ngx-slider-span ngx-slider-tick-value",3,"template","tooltip","placement","content",4,"ngIf"],["class","ngx-slider-span ngx-slider-tick-legend",3,"innerText",4,"ngIf"],["class","ngx-slider-span ngx-slider-tick-legend",3,"innerHTML",4,"ngIf"],[1,"ngx-slider-span","ngx-slider-tick-value",3,"template","tooltip","placement","content"],[1,"ngx-slider-span","ngx-slider-tick-legend",3,"innerText"],[1,"ngx-slider-span","ngx-slider-tick-legend",3,"innerHTML"]],template:function(o,i){1&o&&(v(0,"span",12,0),x(2,"span",13),_(),v(3,"span",14,1),x(5,"span",13),_(),v(6,"span",15,2),x(8,"span",13),_(),v(9,"span",16,3),x(11,"span",17),_(),x(12,"span",18,4)(14,"span",19,5)(16,"span",20,6)(18,"span",21,7)(20,"span",22,8)(22,"span",23,9)(24,"span",24,10),v(26,"span",25,11),Ro(28,JB,5,10,"span",26),_()),2&o&&(f(6),En("ngx-slider-transparent",i.fullBarTransparentClass),f(3),En("ngx-slider-draggable",i.selectionBarDraggableClass),f(2),S("ngStyle",i.barStyle),f(),S("ngStyle",i.minPointerStyle),f(2),pl("display",i.range?"inherit":"none"),S("ngStyle",i.maxPointerStyle),f(12),En("ngx-slider-ticks-values-under",i.ticksUnderValuesClass),S("hidden",!i.showTicks),f(2),S("ngForOf",i.ticks))},dependencies:[qi,ew,_h,iw,to,rg,Qi,QB],styles:['.ngx-slider{display:inline-block;position:relative;height:4px;width:100%;margin:35px 0 15px;vertical-align:middle;-webkit-user-select:none;user-select:none;touch-action:pan-y}  .ngx-slider.with-legend{margin-bottom:40px}  .ngx-slider[disabled]{cursor:not-allowed}  .ngx-slider[disabled] .ngx-slider-pointer{cursor:not-allowed;background-color:#d8e0f3}  .ngx-slider[disabled] .ngx-slider-draggable{cursor:not-allowed}  .ngx-slider[disabled] .ngx-slider-selection{background:#8b91a2}  .ngx-slider[disabled] .ngx-slider-tick{cursor:not-allowed}  .ngx-slider[disabled] .ngx-slider-tick.ngx-slider-selected{background:#8b91a2}  .ngx-slider .ngx-slider-span{white-space:nowrap;position:absolute;display:inline-block}  .ngx-slider .ngx-slider-base{width:100%;height:100%;padding:0}  .ngx-slider .ngx-slider-bar-wrapper{left:0;box-sizing:border-box;margin-top:-16px;padding-top:16px;width:100%;height:32px;z-index:1}  .ngx-slider .ngx-slider-draggable{cursor:move}  .ngx-slider .ngx-slider-bar{left:0;width:100%;height:4px;z-index:1;background:#d8e0f3;-webkit-border-radius:2px;-moz-border-radius:2px;border-radius:2px}  .ngx-slider .ngx-slider-bar-wrapper.ngx-slider-transparent .ngx-slider-bar{background:transparent}  .ngx-slider .ngx-slider-bar-wrapper.ngx-slider-left-out-selection .ngx-slider-bar{background:#df002d}  .ngx-slider .ngx-slider-bar-wrapper.ngx-slider-right-out-selection .ngx-slider-bar{background:#03a688}  .ngx-slider .ngx-slider-selection{z-index:2;background:#0db9f0;-webkit-border-radius:2px;-moz-border-radius:2px;border-radius:2px}  .ngx-slider .ngx-slider-pointer{cursor:pointer;width:32px;height:32px;top:-14px;background-color:#0db9f0;z-index:3;-webkit-border-radius:16px;-moz-border-radius:16px;border-radius:16px}  .ngx-slider .ngx-slider-pointer:after{content:"";width:8px;height:8px;position:absolute;top:12px;left:12px;-webkit-border-radius:4px;-moz-border-radius:4px;border-radius:4px;background:#fff}  .ngx-slider .ngx-slider-pointer:hover:after{background-color:#fff}  .ngx-slider .ngx-slider-pointer.ngx-slider-active{z-index:4}  .ngx-slider .ngx-slider-pointer.ngx-slider-active:after{background-color:#451aff}  .ngx-slider .ngx-slider-bubble{cursor:default;bottom:16px;padding:1px 3px;color:#55637d;font-size:16px}  .ngx-slider .ngx-slider-bubble.ngx-slider-limit{color:#55637d}  .ngx-slider .ngx-slider-ticks{box-sizing:border-box;width:100%;height:0;position:absolute;left:0;top:-3px;margin:0;z-index:1;list-style:none}  .ngx-slider .ngx-slider-ticks-values-under .ngx-slider-tick-value{top:auto;bottom:-36px}  .ngx-slider .ngx-slider-tick{text-align:center;cursor:pointer;width:10px;height:10px;background:#d8e0f3;border-radius:50%;position:absolute;top:0;left:0;margin-left:11px}  .ngx-slider .ngx-slider-tick.ngx-slider-selected{background:#0db9f0}  .ngx-slider .ngx-slider-tick-value{position:absolute;top:-34px;transform:translate(-50%)}  .ngx-slider .ngx-slider-tick-legend{position:absolute;top:24px;transform:translate(-50%);max-width:50px;white-space:normal}  .ngx-slider.vertical{position:relative;width:4px;height:100%;margin:0 20px;padding:0;vertical-align:baseline;touch-action:pan-x}  .ngx-slider.vertical .ngx-slider-base{width:100%;height:100%;padding:0}  .ngx-slider.vertical .ngx-slider-bar-wrapper{top:auto;left:0;margin:0 0 0 -16px;padding:0 0 0 16px;height:100%;width:32px}  .ngx-slider.vertical .ngx-slider-bar{bottom:0;left:auto;width:4px;height:100%}  .ngx-slider.vertical .ngx-slider-pointer{left:-14px!important;top:auto;bottom:0}  .ngx-slider.vertical .ngx-slider-bubble{left:16px!important;bottom:0}  .ngx-slider.vertical .ngx-slider-ticks{height:100%;width:0;left:-3px;top:0;z-index:1}  .ngx-slider.vertical .ngx-slider-tick{vertical-align:middle;margin-left:auto;margin-top:11px}  .ngx-slider.vertical .ngx-slider-tick-value{left:24px;top:auto;transform:translateY(-28%)}  .ngx-slider.vertical .ngx-slider-tick-legend{top:auto;right:24px;transform:translateY(-28%);max-width:none;white-space:nowrap}  .ngx-slider.vertical .ngx-slider-ticks-values-under .ngx-slider-tick-value{bottom:auto;left:auto;right:24px}  .ngx-slider *{transition:none}  .ngx-slider.animate .ngx-slider-bar-wrapper{transition:all linear .3s}  .ngx-slider.animate .ngx-slider-selection{transition:background-color linear .3s}  .ngx-slider.animate .ngx-slider-pointer{transition:all linear .3s}  .ngx-slider.animate .ngx-slider-pointer:after{transition:all linear .3s}  .ngx-slider.animate .ngx-slider-bubble{transition:all linear .3s}  .ngx-slider.animate .ngx-slider-bubble.ngx-slider-limit{transition:opacity linear .3s}  .ngx-slider.animate .ngx-slider-bubble.ngx-slider-combined{transition:opacity linear .3s}  .ngx-slider.animate .ngx-slider-tick{transition:background-color linear .3s}']})}return e})(),e8=(()=>{class e{static \u0275fac=function(o){return new(o||e)};static \u0275mod=Gn({type:e});static \u0275inj=dn({imports:[lw]})}return e})();class i1{constructor(){this.riskHotspotsSettings=null,this.coverageInfoSettings=null}}class t8{constructor(){this.showLineCoverage=!0,this.showBranchCoverage=!0,this.showMethodCoverage=!0,this.showFullMethodCoverage=!0,this.visibleMetrics=[],this.groupingMaximum=0,this.grouping=0,this.historyComparisionDate="",this.historyComparisionType="",this.filter="",this.lineCoverageMin=0,this.lineCoverageMax=100,this.branchCoverageMin=0,this.branchCoverageMax=100,this.methodCoverageMin=0,this.methodCoverageMax=100,this.methodFullCoverageMin=0,this.methodFullCoverageMax=100,this.sortBy="name",this.sortOrder="asc",this.collapseStates=[]}}class n8{constructor(n){this.et="",this.et=n.et,this.cl=n.cl,this.ucl=n.ucl,this.cal=n.cal,this.tl=n.tl,this.lcq=n.lcq,this.cb=n.cb,this.tb=n.tb,this.bcq=n.bcq,this.cm=n.cm,this.fcm=n.fcm,this.tm=n.tm,this.mcq=n.mcq,this.mfcq=n.mfcq}get coverageRatioText(){return 0===this.tl?"-":this.cl+"/"+this.cal}get branchCoverageRatioText(){return 0===this.tb?"-":this.cb+"/"+this.tb}get methodCoverageRatioText(){return 0===this.tm?"-":this.cm+"/"+this.tm}get methodFullCoverageRatioText(){return 0===this.tm?"-":this.fcm+"/"+this.tm}}class xt{static roundNumber(n){return Math.floor(n*Math.pow(10,xt.maximumDecimalPlacesForCoverageQuotas))/Math.pow(10,xt.maximumDecimalPlacesForCoverageQuotas)}static getNthOrLastIndexOf(n,t,o){let i=0,r=-1,s=-1;for(;i<o&&(s=n.indexOf(t,r+1),-1!==s);)r=s,i++;return r}}class r1{constructor(){this.name="",this.coveredLines=0,this.uncoveredLines=0,this.coverableLines=0,this.totalLines=0,this.coveredBranches=0,this.totalBranches=0,this.coveredMethods=0,this.fullyCoveredMethods=0,this.totalMethods=0}get coverage(){return 0===this.coverableLines?NaN:xt.roundNumber(100*this.coveredLines/this.coverableLines)}get coveragePercentage(){return 0===this.coverableLines?"":this.coverage+"%"}get coverageRatioText(){return 0===this.coverableLines?"-":this.coveredLines+"/"+this.coverableLines}get branchCoverage(){return 0===this.totalBranches?NaN:xt.roundNumber(100*this.coveredBranches/this.totalBranches)}get branchCoveragePercentage(){return 0===this.totalBranches?"":this.branchCoverage+"%"}get branchCoverageRatioText(){return 0===this.totalBranches?"-":this.coveredBranches+"/"+this.totalBranches}get methodCoverage(){return 0===this.totalMethods?NaN:xt.roundNumber(100*this.coveredMethods/this.totalMethods)}get methodCoveragePercentage(){return 0===this.totalMethods?"":this.methodCoverage+"%"}get methodCoverageRatioText(){return 0===this.totalMethods?"-":this.coveredMethods+"/"+this.totalMethods}get methodFullCoverage(){return 0===this.totalMethods?NaN:xt.roundNumber(100*this.fullyCoveredMethods/this.totalMethods)}get methodFullCoveragePercentage(){return 0===this.totalMethods?"":this.methodFullCoverage+"%"}get methodFullCoverageRatioText(){return 0===this.totalMethods?"-":this.fullyCoveredMethods+"/"+this.totalMethods}}class sg extends r1{constructor(n,t){super(),this.reportPath="",this.lineCoverageHistory=[],this.branchCoverageHistory=[],this.methodCoverageHistory=[],this.methodFullCoverageHistory=[],this.historicCoverages=[],this.currentHistoricCoverage=null,this.name=n.name,this.reportPath=n.rp?n.rp+t:n.rp,this.coveredLines=n.cl,this.uncoveredLines=n.ucl,this.coverableLines=n.cal,this.totalLines=n.tl,this.coveredBranches=n.cb,this.totalBranches=n.tb,this.coveredMethods=n.cm,this.fullyCoveredMethods=n.fcm,this.totalMethods=n.tm,this.lineCoverageHistory=n.lch,this.branchCoverageHistory=n.bch,this.methodCoverageHistory=n.mch,this.methodFullCoverageHistory=n.mfch,n.hc.forEach(o=>{this.historicCoverages.push(new n8(o))}),this.metrics=n.metrics}get coverage(){return 0===this.coverableLines?NaN:xt.roundNumber(100*this.coveredLines/this.coverableLines)}visible(n){if(""!==n.filter&&-1===this.name.toLowerCase().indexOf(n.filter.toLowerCase()))return!1;let t=this.coverage,o=t;if(t=Number.isNaN(t)?0:t,o=Number.isNaN(o)?100:o,n.lineCoverageMin>t||n.lineCoverageMax<o)return!1;let i=this.branchCoverage,r=i;if(i=Number.isNaN(i)?0:i,r=Number.isNaN(r)?100:r,n.branchCoverageMin>i||n.branchCoverageMax<r)return!1;let s=this.methodCoverage,a=s;if(s=Number.isNaN(s)?0:s,a=Number.isNaN(a)?100:a,n.methodCoverageMin>s||n.methodCoverageMax<a)return!1;let l=this.methodFullCoverage,c=l;if(l=Number.isNaN(l)?0:l,c=Number.isNaN(c)?100:c,n.methodFullCoverageMin>l||n.methodFullCoverageMax<c)return!1;if(""===n.historyComparisionType||null===this.currentHistoricCoverage)return!0;if("allChanges"===n.historyComparisionType){if(this.coveredLines===this.currentHistoricCoverage.cl&&this.uncoveredLines===this.currentHistoricCoverage.ucl&&this.coverableLines===this.currentHistoricCoverage.cal&&this.totalLines===this.currentHistoricCoverage.tl&&this.coveredBranches===this.currentHistoricCoverage.cb&&this.totalBranches===this.currentHistoricCoverage.tb&&this.coveredMethods===this.currentHistoricCoverage.cm&&this.fullyCoveredMethods===this.currentHistoricCoverage.fcm&&this.totalMethods===this.currentHistoricCoverage.tm)return!1}else if("lineCoverageIncreaseOnly"===n.historyComparisionType){let u=this.coverage;if(isNaN(u)||u<=this.currentHistoricCoverage.lcq)return!1}else if("lineCoverageDecreaseOnly"===n.historyComparisionType){let u=this.coverage;if(isNaN(u)||u>=this.currentHistoricCoverage.lcq)return!1}else if("branchCoverageIncreaseOnly"===n.historyComparisionType){let u=this.branchCoverage;if(isNaN(u)||u<=this.currentHistoricCoverage.bcq)return!1}else if("branchCoverageDecreaseOnly"===n.historyComparisionType){let u=this.branchCoverage;if(isNaN(u)||u>=this.currentHistoricCoverage.bcq)return!1}else if("methodCoverageIncreaseOnly"===n.historyComparisionType){let u=this.methodCoverage;if(isNaN(u)||u<=this.currentHistoricCoverage.mcq)return!1}else if("methodCoverageDecreaseOnly"===n.historyComparisionType){let u=this.methodCoverage;if(isNaN(u)||u>=this.currentHistoricCoverage.mcq)return!1}else if("fullMethodCoverageIncreaseOnly"===n.historyComparisionType){let u=this.methodFullCoverage;if(isNaN(u)||u<=this.currentHistoricCoverage.mfcq)return!1}else if("fullMethodCoverageDecreaseOnly"===n.historyComparisionType){let u=this.methodFullCoverage;if(isNaN(u)||u>=this.currentHistoricCoverage.mfcq)return!1}return!0}updateCurrentHistoricCoverage(n){if(this.currentHistoricCoverage=null,""!==n)for(let t=0;t<this.historicCoverages.length;t++)if(this.historicCoverages[t].et===n){this.currentHistoricCoverage=this.historicCoverages[t];break}}}class no extends r1{constructor(n,t){super(),this.subElements=[],this.classes=[],this.collapsed=!1,this.name=n,this.collapsed=n.indexOf("Test")>-1&&null===t}visible(n){if(""!==n.filter&&this.name.toLowerCase().indexOf(n.filter.toLowerCase())>-1)return!0;for(let t=0;t<this.subElements.length;t++)if(this.subElements[t].visible(n))return!0;for(let t=0;t<this.classes.length;t++)if(this.classes[t].visible(n))return!0;return!1}insertClass(n,t){if(this.coveredLines+=n.coveredLines,this.uncoveredLines+=n.uncoveredLines,this.coverableLines+=n.coverableLines,this.totalLines+=n.totalLines,this.coveredBranches+=n.coveredBranches,this.totalBranches+=n.totalBranches,this.coveredMethods+=n.coveredMethods,this.fullyCoveredMethods+=n.fullyCoveredMethods,this.totalMethods+=n.totalMethods,null===t)return void this.classes.push(n);let o=xt.getNthOrLastIndexOf(n.name,".",t);-1===o&&(o=xt.getNthOrLastIndexOf(n.name,"\\",t));let i=-1===o?"-":n.name.substring(0,o);for(let s=0;s<this.subElements.length;s++)if(this.subElements[s].name===i)return void this.subElements[s].insertClass(n,null);let r=new no(i,this);this.subElements.push(r),r.insertClass(n,null)}collapse(){this.collapsed=!0;for(let n=0;n<this.subElements.length;n++)this.subElements[n].collapse()}expand(){this.collapsed=!1;for(let n=0;n<this.subElements.length;n++)this.subElements[n].expand()}toggleCollapse(n){n.preventDefault(),this.collapsed=!this.collapsed}updateCurrentHistoricCoverage(n){for(let t=0;t<this.subElements.length;t++)this.subElements[t].updateCurrentHistoricCoverage(n);for(let t=0;t<this.classes.length;t++)this.classes[t].updateCurrentHistoricCoverage(n)}static sortCodeElementViewModels(n,t,o){let i=o?-1:1,r=o?1:-1;"name"===t?n.sort(function(s,a){return s.name===a.name?0:s.name<a.name?i:r}):"covered"===t?n.sort(function(s,a){return s.coveredLines===a.coveredLines?0:s.coveredLines<a.coveredLines?i:r}):"uncovered"===t?n.sort(function(s,a){return s.uncoveredLines===a.uncoveredLines?0:s.uncoveredLines<a.uncoveredLines?i:r}):"coverable"===t?n.sort(function(s,a){return s.coverableLines===a.coverableLines?0:s.coverableLines<a.coverableLines?i:r}):"total"===t?n.sort(function(s,a){return s.totalLines===a.totalLines?0:s.totalLines<a.totalLines?i:r}):"coverage"===t?n.sort(function(s,a){return s.coverage===a.coverage?0:isNaN(s.coverage)?i:isNaN(a.coverage)?r:s.coverage<a.coverage?i:r}):"covered_branches"===t?n.sort(function(s,a){return s.coveredBranches===a.coveredBranches?0:isNaN(s.coveredBranches)?i:isNaN(a.coveredBranches)?r:s.coveredBranches<a.coveredBranches?i:r}):"total_branches"===t?n.sort(function(s,a){return s.totalBranches===a.totalBranches?0:isNaN(s.totalBranches)?i:isNaN(a.totalBranches)?r:s.totalBranches<a.totalBranches?i:r}):"branchcoverage"===t?n.sort(function(s,a){return s.branchCoverage===a.branchCoverage?0:isNaN(s.branchCoverage)?i:isNaN(a.branchCoverage)?r:s.branchCoverage<a.branchCoverage?i:r}):"covered_methods"===t?n.sort(function(s,a){return s.coveredMethods===a.coveredMethods?0:isNaN(s.coveredMethods)?i:isNaN(a.coveredMethods)?r:s.coveredMethods<a.coveredMethods?i:r}):"fullycovered_methods"===t?n.sort(function(s,a){return s.fullyCoveredMethods===a.fullyCoveredMethods?0:isNaN(s.fullyCoveredMethods)?i:isNaN(a.fullyCoveredMethods)?r:s.fullyCoveredMethods<a.fullyCoveredMethods?i:r}):"total_methods"===t?n.sort(function(s,a){return s.totalMethods===a.totalMethods?0:isNaN(s.totalMethods)?i:isNaN(a.totalMethods)?r:s.totalMethods<a.totalMethods?i:r}):"methodcoverage"===t?n.sort(function(s,a){return s.methodCoverage===a.methodCoverage?0:isNaN(s.methodCoverage)?i:isNaN(a.methodCoverage)?r:s.methodCoverage<a.methodCoverage?i:r}):"methodfullcoverage"===t&&n.sort(function(s,a){return s.methodFullCoverage===a.methodFullCoverage?0:isNaN(s.methodFullCoverage)?i:isNaN(a.methodFullCoverage)?r:s.methodFullCoverage<a.methodFullCoverage?i:r})}changeSorting(n,t){no.sortCodeElementViewModels(this.subElements,n,t);let o=t?-1:1,i=t?1:-1;this.classes.sort("name"===n?function(r,s){return r.name===s.name?0:r.name<s.name?o:i}:"covered"===n?function(r,s){return r.coveredLines===s.coveredLines?0:r.coveredLines<s.coveredLines?o:i}:"uncovered"===n?function(r,s){return r.uncoveredLines===s.uncoveredLines?0:r.uncoveredLines<s.uncoveredLines?o:i}:"coverable"===n?function(r,s){return r.coverableLines===s.coverableLines?0:r.coverableLines<s.coverableLines?o:i}:"total"===n?function(r,s){return r.totalLines===s.totalLines?0:r.totalLines<s.totalLines?o:i}:"coverage"===n?function(r,s){return r.coverage===s.coverage?0:isNaN(r.coverage)?o:isNaN(s.coverage)?i:r.coverage<s.coverage?o:i}:"covered_branches"===n?function(r,s){return r.coveredBranches===s.coveredBranches?0:r.coveredBranches<s.coveredBranches?o:i}:"total_branches"===n?function(r,s){return r.totalBranches===s.totalBranches?0:r.totalBranches<s.totalBranches?o:i}:"branchcoverage"===n?function(r,s){return r.branchCoverage===s.branchCoverage?0:isNaN(r.branchCoverage)?o:isNaN(s.branchCoverage)?i:r.branchCoverage<s.branchCoverage?o:i}:"covered_methods"===n?function(r,s){return r.coveredMethods===s.coveredMethods?0:r.coveredMethods<s.coveredMethods?o:i}:"fullycovered_methods"===n?function(r,s){return r.fullyCoveredMethods===s.fullyCoveredMethods?0:r.fullyCoveredMethods<s.fullyCoveredMethods?o:i}:"total_methods"===n?function(r,s){return r.totalMethods===s.totalMethods?0:r.totalMethods<s.totalMethods?o:i}:"methodcoverage"===n?function(r,s){return r.methodCoverage===s.methodCoverage?0:isNaN(r.methodCoverage)?o:isNaN(s.methodCoverage)?i:r.methodCoverage<s.methodCoverage?o:i}:"methodfullcoverage"===n?function(r,s){return r.methodFullCoverage===s.methodFullCoverage?0:isNaN(r.methodFullCoverage)?o:isNaN(s.methodFullCoverage)?i:r.methodFullCoverage<s.methodFullCoverage?o:i}:function(r,s){const a=r.metrics[n],l=s.metrics[n];return a===l?0:isNaN(a)?o:isNaN(l)?i:a<l?o:i});for(let r=0;r<this.subElements.length;r++)this.subElements[r].changeSorting(n,t)}}let ag=(()=>{class e{get nativeWindow(){return function o8(){return window}()}static#e=this.\u0275fac=function(o){return new(o||e)};static#t=this.\u0275prov=X({token:e,factory:e.\u0275fac})}return e})(),i8=(()=>{class e{constructor(){this.translations={}}static#e=this.\u0275fac=function(o){return new(o||e)};static#t=this.\u0275cmp=Wt({type:e,selectors:[["pro-button"]],inputs:{translations:"translations"},standalone:!1,decls:3,vars:2,consts:[["href","https://reportgenerator.io/pro","target","_blank",1,"pro-button","pro-button-tiny",3,"title"]],template:function(o,i){1&o&&(D(0,"\xa0"),v(1,"a",0),D(2,"PRO"),_()),2&o&&(f(),S("title",Mn(i.translations.methodCoverageProVersion)))},encapsulation:2})}return e})();function r8(e,n){if(1&e){const t=le();v(0,"div",3)(1,"label")(2,"input",4),ze("ngModelChange",function(i){H(t);const r=m();return _e(r.showBranchCoverage,i)||(r.showBranchCoverage=i),B(i)}),j("change",function(){H(t);const i=m();return B(i.showBranchCoverageChange.emit(i.showBranchCoverage))}),_(),D(3),_()()}if(2&e){const t=m();f(2),He("ngModel",t.showBranchCoverage),f(),L(" ",t.translations.branchCoverage)}}function s8(e,n){1&e&&x(0,"pro-button",6),2&e&&S("translations",m().translations)}function a8(e,n){1&e&&x(0,"pro-button",6),2&e&&S("translations",m().translations)}function l8(e,n){1&e&&x(0,"pro-button",6),2&e&&S("translations",m(2).translations)}function c8(e,n){1&e&&(v(0,"a",8),x(1,"i",9),_()),2&e&&S("href",m().$implicit.explanationUrl,jn)}function u8(e,n){if(1&e){const t=le();v(0,"div",3)(1,"label")(2,"input",7),j("change",function(){const i=H(t).$implicit;return B(m(2).toggleMetric(i))}),_(),D(3),_(),D(4,"\xa0"),y(5,c8,2,1,"a",8),_()}if(2&e){const t=n.$implicit,o=m(2);f(2),S("checked",o.isMetricSelected(t))("disabled",!o.methodCoverageAvailable),f(),L(" ",t.name),f(2),C(t.explanationUrl?5:-1)}}function d8(e,n){if(1&e&&(x(0,"br")(1,"br"),v(2,"b"),D(3),_(),y(4,l8,1,1,"pro-button",6),Ke(5,u8,6,4,"div",3,Qe)),2&e){const t=m();f(3),k(t.translations.metrics),f(),C(t.methodCoverageAvailable?-1:4),f(),Xe(t.metrics)}}let f8=(()=>{class e{constructor(){this.visible=!1,this.visibleChange=new me,this.translations={},this.branchCoverageAvailable=!1,this.methodCoverageAvailable=!1,this.metrics=[],this.showLineCoverage=!1,this.showLineCoverageChange=new me,this.showBranchCoverage=!1,this.showBranchCoverageChange=new me,this.showMethodCoverage=!1,this.showMethodCoverageChange=new me,this.showMethodFullCoverage=!1,this.showMethodFullCoverageChange=new me,this.visibleMetrics=[],this.visibleMetricsChange=new me}isMetricSelected(t){return void 0!==this.visibleMetrics.find(o=>o.name===t.name)}toggleMetric(t){let o=this.visibleMetrics.find(i=>i.name===t.name);o?this.visibleMetrics.splice(this.visibleMetrics.indexOf(o),1):this.visibleMetrics.push(t),this.visibleMetrics=[...this.visibleMetrics],this.visibleMetricsChange.emit(this.visibleMetrics)}close(){this.visible=!1,this.visibleChange.emit(this.visible)}cancelEvent(t){t.stopPropagation()}static#e=this.\u0275fac=function(o){return new(o||e)};static#t=this.\u0275cmp=Wt({type:e,selectors:[["popup"]],inputs:{visible:"visible",translations:"translations",branchCoverageAvailable:"branchCoverageAvailable",methodCoverageAvailable:"methodCoverageAvailable",metrics:"metrics",showLineCoverage:"showLineCoverage",showBranchCoverage:"showBranchCoverage",showMethodCoverage:"showMethodCoverage",showMethodFullCoverage:"showMethodFullCoverage",visibleMetrics:"visibleMetrics"},outputs:{visibleChange:"visibleChange",showLineCoverageChange:"showLineCoverageChange",showBranchCoverageChange:"showBranchCoverageChange",showMethodCoverageChange:"showMethodCoverageChange",showMethodFullCoverageChange:"showMethodFullCoverageChange",visibleMetricsChange:"visibleMetricsChange"},standalone:!1,decls:22,vars:13,consts:[[1,"popup-container",3,"click"],[1,"popup",3,"click"],[1,"close",3,"click"],[1,"mt-1"],["type","checkbox",3,"ngModelChange","change","ngModel"],["type","checkbox",3,"ngModelChange","change","ngModel","disabled"],[3,"translations"],["type","checkbox",3,"change","checked","disabled"],["target","_blank",3,"href"],[1,"icon-info-circled"]],template:function(o,i){1&o&&(v(0,"div",0),j("click",function(){return i.close()}),v(1,"div",1),j("click",function(s){return i.cancelEvent(s)}),v(2,"div",2),j("click",function(){return i.close()}),D(3,"X"),_(),v(4,"b"),D(5),_(),v(6,"div",3)(7,"label")(8,"input",4),ze("ngModelChange",function(s){return _e(i.showLineCoverage,s)||(i.showLineCoverage=s),s}),j("change",function(){return i.showLineCoverageChange.emit(i.showLineCoverage)}),_(),D(9),_()(),y(10,r8,4,2,"div",3),v(11,"div",3)(12,"label")(13,"input",5),ze("ngModelChange",function(s){return _e(i.showMethodCoverage,s)||(i.showMethodCoverage=s),s}),j("change",function(){return i.showMethodCoverageChange.emit(i.showMethodCoverage)}),_(),D(14),_(),y(15,s8,1,1,"pro-button",6),_(),v(16,"div",3)(17,"label")(18,"input",5),ze("ngModelChange",function(s){return _e(i.showMethodFullCoverage,s)||(i.showMethodFullCoverage=s),s}),j("change",function(){return i.showMethodFullCoverageChange.emit(i.showMethodFullCoverage)}),_(),D(19),_(),y(20,a8,1,1,"pro-button",6),_(),y(21,d8,7,2),_()()),2&o&&(f(5),k(i.translations.coverageTypes),f(3),He("ngModel",i.showLineCoverage),f(),L(" ",i.translations.coverage),f(),C(i.branchCoverageAvailable?10:-1),f(3),He("ngModel",i.showMethodCoverage),S("disabled",!i.methodCoverageAvailable),f(),L(" ",i.translations.methodCoverage),f(),C(i.methodCoverageAvailable?-1:15),f(3),He("ngModel",i.showMethodFullCoverage),S("disabled",!i.methodCoverageAvailable),f(),L(" ",i.translations.fullMethodCoverage),f(),C(i.methodCoverageAvailable?-1:20),f(),C(i.metrics.length>0?21:-1))},dependencies:[Fh,ql,Is,i8],encapsulation:2})}return e})();function h8(e,n){1&e&&x(0,"td",1)}function g8(e,n){1&e&&x(0,"td"),2&e&&Bt(jt("green ",m().greenClass))}function p8(e,n){1&e&&x(0,"td"),2&e&&Bt(jt("red ",m().redClass))}let s1=(()=>{class e{constructor(){this.grayVisible=!0,this.greenVisible=!1,this.redVisible=!1,this.greenClass="",this.redClass="",this._percentage=NaN}get percentage(){return this._percentage}set percentage(t){this._percentage=t,this.grayVisible=isNaN(t),this.greenVisible=!isNaN(t)&&Math.round(t)>0,this.redVisible=!isNaN(t)&&100-Math.round(t)>0,this.greenClass="covered"+Math.round(t),this.redClass="covered"+(100-Math.round(t))}static#e=this.\u0275fac=function(o){return new(o||e)};static#t=this.\u0275cmp=Wt({type:e,selectors:[["coverage-bar"]],inputs:{percentage:"percentage"},standalone:!1,decls:4,vars:3,consts:[[1,"coverage"],[1,"gray","covered100"],[3,"class"]],template:function(o,i){1&o&&(v(0,"table",0),y(1,h8,1,0,"td",1),y(2,g8,1,3,"td",2),y(3,p8,1,3,"td",2),_()),2&o&&(f(),C(i.grayVisible?1:-1),f(),C(i.greenVisible?2:-1),f(),C(i.redVisible?3:-1))},encapsulation:2,changeDetection:0})}return e})();const m8=["codeelement-row",""],_8=(e,n)=>({"icon-plus":e,"icon-minus":n});function v8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.coveredLines)}}function y8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.uncoveredLines)}}function C8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.coverableLines)}}function b8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.totalLines)}}function D8(e,n){if(1&e&&(v(0,"th",3),D(1),_()),2&e){const t=m();S("title",t.element.coverageRatioText),f(),k(t.element.coveragePercentage)}}function w8(e,n){if(1&e&&(v(0,"th",2),x(1,"coverage-bar",4),_()),2&e){const t=m();f(),S("percentage",t.element.coverage)}}function E8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.coveredBranches)}}function I8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.totalBranches)}}function M8(e,n){if(1&e&&(v(0,"th",3),D(1),_()),2&e){const t=m();S("title",t.element.branchCoverageRatioText),f(),k(t.element.branchCoveragePercentage)}}function T8(e,n){if(1&e&&(v(0,"th",2),x(1,"coverage-bar",4),_()),2&e){const t=m();f(),S("percentage",t.element.branchCoverage)}}function S8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.coveredMethods)}}function O8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.totalMethods)}}function N8(e,n){if(1&e&&(v(0,"th",3),D(1),_()),2&e){const t=m();S("title",t.element.methodCoverageRatioText),f(),k(t.element.methodCoveragePercentage)}}function A8(e,n){if(1&e&&(v(0,"th",2),x(1,"coverage-bar",4),_()),2&e){const t=m();f(),S("percentage",t.element.methodCoverage)}}function x8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.fullyCoveredMethods)}}function k8(e,n){if(1&e&&(v(0,"th",2),D(1),_()),2&e){const t=m();f(),k(t.element.totalMethods)}}function R8(e,n){if(1&e&&(v(0,"th",3),D(1),_()),2&e){const t=m();S("title",t.element.methodFullCoverageRatioText),f(),k(t.element.methodFullCoveragePercentage)}}function F8(e,n){if(1&e&&(v(0,"th",2),x(1,"coverage-bar",4),_()),2&e){const t=m();f(),S("percentage",t.element.methodFullCoverage)}}function L8(e,n){1&e&&x(0,"th",2)}let P8=(()=>{class e{constructor(){this.collapsed=!1,this.lineCoverageAvailable=!1,this.branchCoverageAvailable=!1,this.methodCoverageAvailable=!1,this.methodFullCoverageAvailable=!1,this.visibleMetrics=[]}static#e=this.\u0275fac=function(o){return new(o||e)};static#t=this.\u0275cmp=Wt({type:e,selectors:[["","codeelement-row",""]],inputs:{element:"element",collapsed:"collapsed",lineCoverageAvailable:"lineCoverageAvailable",branchCoverageAvailable:"branchCoverageAvailable",methodCoverageAvailable:"methodCoverageAvailable",methodFullCoverageAvailable:"methodFullCoverageAvailable",visibleMetrics:"visibleMetrics"},standalone:!1,attrs:m8,decls:24,vars:23,consts:[["href","#",3,"click"],[3,"ngClass"],[1,"right"],[1,"right",3,"title"],[3,"percentage"]],template:function(o,i){1&o&&(v(0,"th")(1,"a",0),j("click",function(s){return i.element.toggleCollapse(s)}),x(2,"i",1),D(3),_()(),y(4,v8,2,1,"th",2),y(5,y8,2,1,"th",2),y(6,C8,2,1,"th",2),y(7,b8,2,1,"th",2),y(8,D8,2,2,"th",3),y(9,w8,2,1,"th",2),y(10,E8,2,1,"th",2),y(11,I8,2,1,"th",2),y(12,M8,2,2,"th",3),y(13,T8,2,1,"th",2),y(14,S8,2,1,"th",2),y(15,O8,2,1,"th",2),y(16,N8,2,2,"th",3),y(17,A8,2,1,"th",2),y(18,x8,2,1,"th",2),y(19,k8,2,1,"th",2),y(20,R8,2,2,"th",3),y(21,F8,2,1,"th",2),Ke(22,L8,1,0,"th",2,Qe)),2&o&&(f(2),S("ngClass",jf(20,_8,i.element.collapsed,!i.element.collapsed)),f(),L("\n",i.element.name),f(),C(i.lineCoverageAvailable?4:-1),f(),C(i.lineCoverageAvailable?5:-1),f(),C(i.lineCoverageAvailable?6:-1),f(),C(i.lineCoverageAvailable?7:-1),f(),C(i.lineCoverageAvailable?8:-1),f(),C(i.lineCoverageAvailable?9:-1),f(),C(i.branchCoverageAvailable?10:-1),f(),C(i.branchCoverageAvailable?11:-1),f(),C(i.branchCoverageAvailable?12:-1),f(),C(i.branchCoverageAvailable?13:-1),f(),C(i.methodCoverageAvailable?14:-1),f(),C(i.methodCoverageAvailable?15:-1),f(),C(i.methodCoverageAvailable?16:-1),f(),C(i.methodCoverageAvailable?17:-1),f(),C(i.methodFullCoverageAvailable?18:-1),f(),C(i.methodFullCoverageAvailable?19:-1),f(),C(i.methodFullCoverageAvailable?20:-1),f(),C(i.methodFullCoverageAvailable?21:-1),f(),Xe(i.visibleMetrics))},dependencies:[qi,s1],encapsulation:2,changeDetection:0})}return e})();const V8=["coverage-history-chart",""];let H8=(()=>{class e{constructor(){this.path=null,this._historicCoverages=[]}get historicCoverages(){return this._historicCoverages}set historicCoverages(t){if(this._historicCoverages=t,t.length>1){let o="";for(let i=0;i<t.length;i++)o+=0===i?"M":"L",o+=`${xt.roundNumber(30*i/(t.length-1))}`,o+=`,${xt.roundNumber(18-18*t[i]/100)}`;this.path=o}else this.path=null}static#e=this.\u0275fac=function(o){return new(o||e)};static#t=this.\u0275cmp=Wt({type:e,selectors:[["","coverage-history-chart",""]],inputs:{historicCoverages:"historicCoverages"},standalone:!1,attrs:V8,decls:3,vars:1,consts:[["width","30","height","18",1,"ct-chart-line"],[1,"ct-series","ct-series-a"],[1,"ct-line"]],template:function(o,i){1&o&&(function pp(){z.lFrame.currentNamespace="svg"}(),v(0,"svg",0)(1,"g",1),x(2,"path",2),_()()),2&o&&(f(2),lt("d",i.path))},encapsulation:2,changeDetection:0})}return e})();const B8=["class-row",""],uc=e=>({historiccoverageoffset:e});function j8(e,n){if(1&e&&(v(0,"a",0),D(1),_()),2&e){const t=m();S("href",t.clazz.reportPath,jn),f(),k(t.clazz.name)}}function U8(e,n){1&e&&D(0),2&e&&L(" ",m().clazz.name," ")}function $8(e,n){if(1&e&&(v(0,"div"),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);Bt(jt("currenthistory ",t.getClassName(t.clazz.coveredLines,t.clazz.currentHistoricCoverage.cl))),f(),L(" ",t.clazz.coveredLines," "),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),L(" ",t.clazz.currentHistoricCoverage.cl," ")}}function z8(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.coveredLines," ")}function G8(e,n){if(1&e&&(v(0,"td",1),y(1,$8,4,6),y(2,z8,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function q8(e,n){if(1&e&&(v(0,"div"),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);Bt(jt("currenthistory ",t.getClassName(t.clazz.currentHistoricCoverage.ucl,t.clazz.uncoveredLines))),f(),L(" ",t.clazz.uncoveredLines," "),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),L(" ",t.clazz.currentHistoricCoverage.ucl," ")}}function W8(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.uncoveredLines," ")}function Y8(e,n){if(1&e&&(v(0,"td",1),y(1,q8,4,6),y(2,W8,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function J8(e,n){if(1&e&&(v(0,"div",4),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);f(),k(t.clazz.coverableLines),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),k(t.clazz.currentHistoricCoverage.cal)}}function Z8(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.coverableLines," ")}function Q8(e,n){if(1&e&&(v(0,"td",1),y(1,J8,4,3),y(2,Z8,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function K8(e,n){if(1&e&&(v(0,"div",4),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);f(),k(t.clazz.totalLines),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),k(t.clazz.currentHistoricCoverage.tl)}}function X8(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.totalLines," ")}function ej(e,n){if(1&e&&(v(0,"td",1),y(1,K8,4,3),y(2,X8,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function tj(e,n){if(1&e&&x(0,"div",5),2&e){const t=m(2);S("title",Mn(t.translations.history+": "+t.translations.coverage))("historicCoverages",t.clazz.lineCoverageHistory)("ngClass",ji(4,uc,null!==t.clazz.currentHistoricCoverage))}}function nj(e,n){if(1&e&&(v(0,"div"),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);Bt(jt("currenthistory ",t.getClassName(t.clazz.coverage,t.clazz.currentHistoricCoverage.lcq))),f(),L(" ",t.clazz.coveragePercentage," "),f(),S("title",t.clazz.currentHistoricCoverage.et+": "+t.clazz.currentHistoricCoverage.coverageRatioText),f(),L("",t.clazz.currentHistoricCoverage.lcq,"%")}}function oj(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.coveragePercentage," ")}function ij(e,n){if(1&e&&(v(0,"td",2),y(1,tj,1,6,"div",5),y(2,nj,4,6),y(3,oj,1,1),_()),2&e){const t=m();S("title",t.clazz.coverageRatioText),f(),C(t.clazz.lineCoverageHistory.length>1?1:-1),f(),C(null!==t.clazz.currentHistoricCoverage?2:-1),f(),C(null===t.clazz.currentHistoricCoverage?3:-1)}}function rj(e,n){if(1&e&&(v(0,"td",1),x(1,"coverage-bar",6),_()),2&e){const t=m();f(),S("percentage",t.clazz.coverage)}}function sj(e,n){if(1&e&&(v(0,"div"),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);Bt(jt("currenthistory ",t.getClassName(t.clazz.coveredBranches,t.clazz.currentHistoricCoverage.cb))),f(),L(" ",t.clazz.coveredBranches," "),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),L(" ",t.clazz.currentHistoricCoverage.cb," ")}}function aj(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.coveredBranches," ")}function lj(e,n){if(1&e&&(v(0,"td",1),y(1,sj,4,6),y(2,aj,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function cj(e,n){if(1&e&&(v(0,"div",4),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);f(),k(t.clazz.totalBranches),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),k(t.clazz.currentHistoricCoverage.tb)}}function uj(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.totalBranches," ")}function dj(e,n){if(1&e&&(v(0,"td",1),y(1,cj,4,3),y(2,uj,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function fj(e,n){if(1&e&&x(0,"div",7),2&e){const t=m(2);S("title",Mn(t.translations.history+": "+t.translations.branchCoverage))("historicCoverages",t.clazz.branchCoverageHistory)("ngClass",ji(4,uc,null!==t.clazz.currentHistoricCoverage))}}function hj(e,n){if(1&e&&(v(0,"div"),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);Bt(jt("currenthistory ",t.getClassName(t.clazz.branchCoverage,t.clazz.currentHistoricCoverage.bcq))),f(),L(" ",t.clazz.branchCoveragePercentage," "),f(),S("title",t.clazz.currentHistoricCoverage.et+": "+t.clazz.currentHistoricCoverage.branchCoverageRatioText),f(),L("",t.clazz.currentHistoricCoverage.bcq,"%")}}function gj(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.branchCoveragePercentage," ")}function pj(e,n){if(1&e&&(v(0,"td",2),y(1,fj,1,6,"div",7),y(2,hj,4,6),y(3,gj,1,1),_()),2&e){const t=m();S("title",t.clazz.branchCoverageRatioText),f(),C(t.clazz.branchCoverageHistory.length>1?1:-1),f(),C(null!==t.clazz.currentHistoricCoverage?2:-1),f(),C(null===t.clazz.currentHistoricCoverage?3:-1)}}function mj(e,n){if(1&e&&(v(0,"td",1),x(1,"coverage-bar",6),_()),2&e){const t=m();f(),S("percentage",t.clazz.branchCoverage)}}function _j(e,n){if(1&e&&(v(0,"div"),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);Bt(jt("currenthistory ",t.getClassName(t.clazz.coveredMethods,t.clazz.currentHistoricCoverage.cm))),f(),L(" ",t.clazz.coveredMethods," "),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),L(" ",t.clazz.currentHistoricCoverage.cm," ")}}function vj(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.coveredMethods," ")}function yj(e,n){if(1&e&&(v(0,"td",1),y(1,_j,4,6),y(2,vj,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function Cj(e,n){if(1&e&&(v(0,"div",4),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);f(),k(t.clazz.totalMethods),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),k(t.clazz.currentHistoricCoverage.tm)}}function bj(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.totalMethods," ")}function Dj(e,n){if(1&e&&(v(0,"td",1),y(1,Cj,4,3),y(2,bj,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function wj(e,n){if(1&e&&x(0,"div",8),2&e){const t=m(2);S("title",Mn(t.translations.history+": "+t.translations.methodCoverage))("historicCoverages",t.clazz.methodCoverageHistory)("ngClass",ji(4,uc,null!==t.clazz.currentHistoricCoverage))}}function Ej(e,n){if(1&e&&(v(0,"div"),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);Bt(jt("currenthistory ",t.getClassName(t.clazz.methodCoverage,t.clazz.currentHistoricCoverage.mcq))),f(),L(" ",t.clazz.methodCoveragePercentage," "),f(),S("title",t.clazz.currentHistoricCoverage.et+": "+t.clazz.currentHistoricCoverage.methodCoverageRatioText),f(),L("",t.clazz.currentHistoricCoverage.mcq,"%")}}function Ij(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.methodCoveragePercentage," ")}function Mj(e,n){if(1&e&&(v(0,"td",2),y(1,wj,1,6,"div",8),y(2,Ej,4,6),y(3,Ij,1,1),_()),2&e){const t=m();S("title",t.clazz.methodCoverageRatioText),f(),C(t.clazz.methodCoverageHistory.length>1?1:-1),f(),C(null!==t.clazz.currentHistoricCoverage?2:-1),f(),C(null===t.clazz.currentHistoricCoverage?3:-1)}}function Tj(e,n){if(1&e&&(v(0,"td",1),x(1,"coverage-bar",6),_()),2&e){const t=m();f(),S("percentage",t.clazz.methodCoverage)}}function Sj(e,n){if(1&e&&(v(0,"div"),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);Bt(jt("currenthistory ",t.getClassName(t.clazz.fullyCoveredMethods,t.clazz.currentHistoricCoverage.fcm))),f(),L(" ",t.clazz.fullyCoveredMethods," "),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),L(" ",t.clazz.currentHistoricCoverage.fcm," ")}}function Oj(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.fullyCoveredMethods," ")}function Nj(e,n){if(1&e&&(v(0,"td",1),y(1,Sj,4,6),y(2,Oj,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function Aj(e,n){if(1&e&&(v(0,"div",4),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);f(),k(t.clazz.totalMethods),f(),S("title",t.clazz.currentHistoricCoverage.et),f(),k(t.clazz.currentHistoricCoverage.tm)}}function xj(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.totalMethods," ")}function kj(e,n){if(1&e&&(v(0,"td",1),y(1,Aj,4,3),y(2,xj,1,1),_()),2&e){const t=m();f(),C(null!==t.clazz.currentHistoricCoverage?1:-1),f(),C(null===t.clazz.currentHistoricCoverage?2:-1)}}function Rj(e,n){if(1&e&&x(0,"div",9),2&e){const t=m(2);S("title",Mn(t.translations.history+": "+t.translations.fullMethodCoverage))("historicCoverages",t.clazz.methodFullCoverageHistory)("ngClass",ji(4,uc,null!==t.clazz.currentHistoricCoverage))}}function Fj(e,n){if(1&e&&(v(0,"div"),D(1),_(),v(2,"div",3),D(3),_()),2&e){const t=m(2);Bt(jt("currenthistory ",t.getClassName(t.clazz.methodFullCoverage,t.clazz.currentHistoricCoverage.mfcq))),f(),L(" ",t.clazz.methodFullCoveragePercentage," "),f(),S("title",t.clazz.currentHistoricCoverage.et+": "+t.clazz.currentHistoricCoverage.methodFullCoverageRatioText),f(),L("",t.clazz.currentHistoricCoverage.mfcq,"%")}}function Lj(e,n){1&e&&D(0),2&e&&L(" ",m(2).clazz.methodFullCoveragePercentage," ")}function Pj(e,n){if(1&e&&(v(0,"td",2),y(1,Rj,1,6,"div",9),y(2,Fj,4,6),y(3,Lj,1,1),_()),2&e){const t=m();S("title",t.clazz.methodFullCoverageRatioText),f(),C(t.clazz.methodFullCoverageHistory.length>1?1:-1),f(),C(null!==t.clazz.currentHistoricCoverage?2:-1),f(),C(null===t.clazz.currentHistoricCoverage?3:-1)}}function Vj(e,n){if(1&e&&(v(0,"td",1),x(1,"coverage-bar",6),_()),2&e){const t=m();f(),S("percentage",t.clazz.methodFullCoverage)}}function Hj(e,n){if(1&e&&(v(0,"td",1),D(1),_()),2&e){const t=n.$implicit,o=m();f(),k(o.clazz.metrics[t.abbreviation])}}let Bj=(()=>{class e{constructor(){this.translations={},this.lineCoverageAvailable=!1,this.branchCoverageAvailable=!1,this.methodCoverageAvailable=!1,this.methodFullCoverageAvailable=!1,this.visibleMetrics=[],this.historyComparisionDate=""}getClassName(t,o){return t>o?"lightgreen":t<o?"lightred":"lightgraybg"}static#e=this.\u0275fac=function(o){return new(o||e)};static#t=this.\u0275cmp=Wt({type:e,selectors:[["","class-row",""]],inputs:{clazz:"clazz",translations:"translations",lineCoverageAvailable:"lineCoverageAvailable",branchCoverageAvailable:"branchCoverageAvailable",methodCoverageAvailable:"methodCoverageAvailable",methodFullCoverageAvailable:"methodFullCoverageAvailable",visibleMetrics:"visibleMetrics",historyComparisionDate:"historyComparisionDate"},standalone:!1,attrs:B8,decls:23,vars:20,consts:[[3,"href"],[1,"right"],[1,"right",3,"title"],[3,"title"],[1,"currenthistory"],["coverage-history-chart","",1,"tinylinecoveragechart","ct-chart",3,"historicCoverages","ngClass","title"],[3,"percentage"],["coverage-history-chart","",1,"tinybranchcoveragechart","ct-chart",3,"historicCoverages","ngClass","title"],["coverage-history-chart","",1,"tinymethodcoveragechart","ct-chart",3,"historicCoverages","ngClass","title"],["coverage-history-chart","",1,"tinyfullmethodcoveragechart","ct-chart",3,"historicCoverages","ngClass","title"]],template:function(o,i){1&o&&(v(0,"td"),y(1,j8,2,2,"a",0),y(2,U8,1,1),_(),y(3,G8,3,2,"td",1),y(4,Y8,3,2,"td",1),y(5,Q8,3,2,"td",1),y(6,ej,3,2,"td",1),y(7,ij,4,4,"td",2),y(8,rj,2,1,"td",1),y(9,lj,3,2,"td",1),y(10,dj,3,2,"td",1),y(11,pj,4,4,"td",2),y(12,mj,2,1,"td",1),y(13,yj,3,2,"td",1),y(14,Dj,3,2,"td",1),y(15,Mj,4,4,"td",2),y(16,Tj,2,1,"td",1),y(17,Nj,3,2,"td",1),y(18,kj,3,2,"td",1),y(19,Pj,4,4,"td",2),y(20,Vj,2,1,"td",1),Ke(21,Hj,2,1,"td",1,Qe)),2&o&&(f(),C(""!==i.clazz.reportPath?1:-1),f(),C(""===i.clazz.reportPath?2:-1),f(),C(i.lineCoverageAvailable?3:-1),f(),C(i.lineCoverageAvailable?4:-1),f(),C(i.lineCoverageAvailable?5:-1),f(),C(i.lineCoverageAvailable?6:-1),f(),C(i.lineCoverageAvailable?7:-1),f(),C(i.lineCoverageAvailable?8:-1),f(),C(i.branchCoverageAvailable?9:-1),f(),C(i.branchCoverageAvailable?10:-1),f(),C(i.branchCoverageAvailable?11:-1),f(),C(i.branchCoverageAvailable?12:-1),f(),C(i.methodCoverageAvailable?13:-1),f(),C(i.methodCoverageAvailable?14:-1),f(),C(i.methodCoverageAvailable?15:-1),f(),C(i.methodCoverageAvailable?16:-1),f(),C(i.methodFullCoverageAvailable?17:-1),f(),C(i.methodFullCoverageAvailable?18:-1),f(),C(i.methodFullCoverageAvailable?19:-1),f(),C(i.methodFullCoverageAvailable?20:-1),f(),Xe(i.visibleMetrics))},dependencies:[qi,H8,s1],encapsulation:2,changeDetection:0})}return e})();const tt=(e,n,t)=>({"icon-up-dir_active":e,"icon-down-dir_active":n,"icon-up-down-dir":t});function jj(e,n){if(1&e){const t=le();v(0,"popup",27),ze("visibleChange",function(i){H(t);const r=m(2);return _e(r.popupVisible,i)||(r.popupVisible=i),B(i)})("showLineCoverageChange",function(i){H(t);const r=m(2);return _e(r.settings.showLineCoverage,i)||(r.settings.showLineCoverage=i),B(i)})("showBranchCoverageChange",function(i){H(t);const r=m(2);return _e(r.settings.showBranchCoverage,i)||(r.settings.showBranchCoverage=i),B(i)})("showMethodCoverageChange",function(i){H(t);const r=m(2);return _e(r.settings.showMethodCoverage,i)||(r.settings.showMethodCoverage=i),B(i)})("showMethodFullCoverageChange",function(i){H(t);const r=m(2);return _e(r.settings.showFullMethodCoverage,i)||(r.settings.showFullMethodCoverage=i),B(i)})("visibleMetricsChange",function(i){H(t);const r=m(2);return _e(r.settings.visibleMetrics,i)||(r.settings.visibleMetrics=i),B(i)}),_()}if(2&e){const t=m(2);He("visible",t.popupVisible),S("translations",t.translations)("branchCoverageAvailable",t.branchCoverageAvailable)("methodCoverageAvailable",t.methodCoverageAvailable)("metrics",t.metrics),He("showLineCoverage",t.settings.showLineCoverage)("showBranchCoverage",t.settings.showBranchCoverage)("showMethodCoverage",t.settings.showMethodCoverage)("showMethodFullCoverage",t.settings.showFullMethodCoverage)("visibleMetrics",t.settings.visibleMetrics)}}function Uj(e,n){1&e&&D(0),2&e&&L(" ",m(2).translations.noGrouping," ")}function $j(e,n){1&e&&D(0),2&e&&L(" ",m(2).translations.byAssembly," ")}function zj(e,n){if(1&e&&D(0),2&e){const t=m(2);L(" ",t.translations.byNamespace+" "+t.settings.grouping," ")}}function Gj(e,n){if(1&e&&(v(0,"option",30),D(1),_()),2&e){const t=n.$implicit;S("value",t),f(),k(t)}}function qj(e,n){1&e&&x(0,"br")}function Wj(e,n){if(1&e&&(v(0,"option",34),D(1),_()),2&e){const t=m(4);f(),L(" ",t.translations.branchCoverageIncreaseOnly," ")}}function Yj(e,n){if(1&e&&(v(0,"option",35),D(1),_()),2&e){const t=m(4);f(),L(" ",t.translations.branchCoverageDecreaseOnly," ")}}function Jj(e,n){if(1&e&&(v(0,"option",36),D(1),_()),2&e){const t=m(4);f(),L(" ",t.translations.methodCoverageIncreaseOnly," ")}}function Zj(e,n){if(1&e&&(v(0,"option",37),D(1),_()),2&e){const t=m(4);f(),L(" ",t.translations.methodCoverageDecreaseOnly," ")}}function Qj(e,n){if(1&e&&(v(0,"option",38),D(1),_()),2&e){const t=m(4);f(),L(" ",t.translations.fullMethodCoverageIncreaseOnly," ")}}function Kj(e,n){if(1&e&&(v(0,"option",39),D(1),_()),2&e){const t=m(4);f(),L(" ",t.translations.fullMethodCoverageDecreaseOnly," ")}}function Xj(e,n){if(1&e){const t=le();v(0,"div")(1,"select",28),ze("ngModelChange",function(i){H(t);const r=m(3);return _e(r.settings.historyComparisionType,i)||(r.settings.historyComparisionType=i),B(i)}),v(2,"option",29),D(3),_(),v(4,"option",31),D(5),_(),v(6,"option",32),D(7),_(),v(8,"option",33),D(9),_(),y(10,Wj,2,1,"option",34),y(11,Yj,2,1,"option",35),y(12,Jj,2,1,"option",36),y(13,Zj,2,1,"option",37),y(14,Qj,2,1,"option",38),y(15,Kj,2,1,"option",39),_()()}if(2&e){const t=m(3);f(),He("ngModel",t.settings.historyComparisionType),f(2),k(t.translations.filter),f(2),k(t.translations.allChanges),f(2),k(t.translations.lineCoverageIncreaseOnly),f(2),k(t.translations.lineCoverageDecreaseOnly),f(),C(t.branchCoverageAvailable?10:-1),f(),C(t.branchCoverageAvailable?11:-1),f(),C(t.methodCoverageAvailable?12:-1),f(),C(t.methodCoverageAvailable?13:-1),f(),C(t.methodCoverageAvailable?14:-1),f(),C(t.methodCoverageAvailable?15:-1)}}function e3(e,n){if(1&e){const t=le();v(0,"div"),D(1),v(2,"select",28),ze("ngModelChange",function(i){H(t);const r=m(2);return _e(r.settings.historyComparisionDate,i)||(r.settings.historyComparisionDate=i),B(i)}),j("ngModelChange",function(){return H(t),B(m(2).updateCurrentHistoricCoverage())}),v(3,"option",29),D(4),_(),Ke(5,Gj,2,2,"option",30,Qe),_()(),y(7,qj,1,0,"br"),y(8,Xj,16,11,"div")}if(2&e){const t=m(2);f(),L(" ",t.translations.compareHistory," "),f(),He("ngModel",t.settings.historyComparisionDate),f(2),k(t.translations.date),f(),Xe(t.historicCoverageExecutionTimes),f(2),C(""!==t.settings.historyComparisionDate?7:-1),f(),C(""!==t.settings.historyComparisionDate?8:-1)}}function t3(e,n){1&e&&x(0,"col",12)}function n3(e,n){1&e&&x(0,"col",13)}function o3(e,n){1&e&&x(0,"col",14)}function i3(e,n){1&e&&x(0,"col",15)}function r3(e,n){1&e&&x(0,"col",16)}function s3(e,n){1&e&&x(0,"col",17)}function a3(e,n){1&e&&x(0,"col",12)}function l3(e,n){1&e&&x(0,"col",15)}function c3(e,n){1&e&&x(0,"col",16)}function u3(e,n){1&e&&x(0,"col",17)}function d3(e,n){1&e&&x(0,"col",12)}function f3(e,n){1&e&&x(0,"col",15)}function h3(e,n){1&e&&x(0,"col",16)}function g3(e,n){1&e&&x(0,"col",17)}function p3(e,n){1&e&&x(0,"col",12)}function m3(e,n){1&e&&x(0,"col",15)}function _3(e,n){1&e&&x(0,"col",16)}function v3(e,n){1&e&&x(0,"col",17)}function y3(e,n){1&e&&x(0,"col",17)}function C3(e,n){if(1&e&&(v(0,"th",19),D(1),_()),2&e){const t=m(2);f(),k(t.translations.coverage)}}function b3(e,n){if(1&e&&(v(0,"th",20),D(1),_()),2&e){const t=m(2);f(),k(t.translations.branchCoverage)}}function D3(e,n){if(1&e&&(v(0,"th",20),D(1),_()),2&e){const t=m(2);f(),k(t.translations.methodCoverage)}}function w3(e,n){if(1&e&&(v(0,"th",20),D(1),_()),2&e){const t=m(2);f(),k(t.translations.fullMethodCoverage)}}function E3(e,n){if(1&e&&(v(0,"th",21),D(1),_()),2&e){const t=m(2);lt("colspan",t.settings.visibleMetrics.length),f(),k(t.translations.metrics)}}function I3(e,n){if(1&e){const t=le();v(0,"td",19)(1,"ngx-slider",40),ze("valueChange",function(i){H(t);const r=m(2);return _e(r.settings.lineCoverageMin,i)||(r.settings.lineCoverageMin=i),B(i)})("highValueChange",function(i){H(t);const r=m(2);return _e(r.settings.lineCoverageMax,i)||(r.settings.lineCoverageMax=i),B(i)}),_()()}if(2&e){const t=m(2);f(),He("value",t.settings.lineCoverageMin)("highValue",t.settings.lineCoverageMax),S("options",t.sliderOptions)}}function M3(e,n){if(1&e){const t=le();v(0,"td",20)(1,"ngx-slider",40),ze("valueChange",function(i){H(t);const r=m(2);return _e(r.settings.branchCoverageMin,i)||(r.settings.branchCoverageMin=i),B(i)})("highValueChange",function(i){H(t);const r=m(2);return _e(r.settings.branchCoverageMax,i)||(r.settings.branchCoverageMax=i),B(i)}),_()()}if(2&e){const t=m(2);f(),He("value",t.settings.branchCoverageMin)("highValue",t.settings.branchCoverageMax),S("options",t.sliderOptions)}}function T3(e,n){if(1&e){const t=le();v(0,"td",20)(1,"ngx-slider",40),ze("valueChange",function(i){H(t);const r=m(2);return _e(r.settings.methodCoverageMin,i)||(r.settings.methodCoverageMin=i),B(i)})("highValueChange",function(i){H(t);const r=m(2);return _e(r.settings.methodCoverageMax,i)||(r.settings.methodCoverageMax=i),B(i)}),_()()}if(2&e){const t=m(2);f(),He("value",t.settings.methodCoverageMin)("highValue",t.settings.methodCoverageMax),S("options",t.sliderOptions)}}function S3(e,n){if(1&e){const t=le();v(0,"td",20)(1,"ngx-slider",40),ze("valueChange",function(i){H(t);const r=m(2);return _e(r.settings.methodFullCoverageMin,i)||(r.settings.methodFullCoverageMin=i),B(i)})("highValueChange",function(i){H(t);const r=m(2);return _e(r.settings.methodFullCoverageMax,i)||(r.settings.methodFullCoverageMax=i),B(i)}),_()()}if(2&e){const t=m(2);f(),He("value",t.settings.methodFullCoverageMin)("highValue",t.settings.methodFullCoverageMax),S("options",t.sliderOptions)}}function O3(e,n){1&e&&x(0,"td",21),2&e&&lt("colspan",m(2).settings.visibleMetrics.length)}function N3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("covered",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"covered"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"covered"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"covered"!==t.settings.sortBy)),f(),k(t.translations.covered)}}function A3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("uncovered",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"uncovered"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"uncovered"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"uncovered"!==t.settings.sortBy)),f(),k(t.translations.uncovered)}}function x3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("coverable",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"coverable"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"coverable"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"coverable"!==t.settings.sortBy)),f(),k(t.translations.coverable)}}function k3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("total",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"total"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"total"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"total"!==t.settings.sortBy)),f(),k(t.translations.total)}}function R3(e,n){if(1&e){const t=le();v(0,"th",26)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("coverage",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"coverage"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"coverage"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"coverage"!==t.settings.sortBy)),f(),k(t.translations.percentage)}}function F3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("covered_branches",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"covered_branches"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"covered_branches"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"covered_branches"!==t.settings.sortBy)),f(),k(t.translations.covered)}}function L3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("total_branches",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"total_branches"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"total_branches"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"total_branches"!==t.settings.sortBy)),f(),k(t.translations.total)}}function P3(e,n){if(1&e){const t=le();v(0,"th",26)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("branchcoverage",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"branchcoverage"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"branchcoverage"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"branchcoverage"!==t.settings.sortBy)),f(),k(t.translations.percentage)}}function V3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("covered_methods",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"covered_methods"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"covered_methods"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"covered_methods"!==t.settings.sortBy)),f(),k(t.translations.covered)}}function H3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("total_methods",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"total_methods"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"total_methods"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"total_methods"!==t.settings.sortBy)),f(),k(t.translations.total)}}function B3(e,n){if(1&e){const t=le();v(0,"th",26)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("methodcoverage",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"methodcoverage"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"methodcoverage"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"methodcoverage"!==t.settings.sortBy)),f(),k(t.translations.percentage)}}function j3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("fullycovered_methods",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"fullycovered_methods"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"fullycovered_methods"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"fullycovered_methods"!==t.settings.sortBy)),f(),k(t.translations.covered)}}function U3(e,n){if(1&e){const t=le();v(0,"th",25)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("total_methods",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"total_methods"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"total_methods"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"total_methods"!==t.settings.sortBy)),f(),k(t.translations.total)}}function $3(e,n){if(1&e){const t=le();v(0,"th",26)(1,"a",2),j("click",function(i){return H(t),B(m(2).updateSorting("methodfullcoverage",i))}),x(2,"i",24),D(3),_()()}if(2&e){const t=m(2);f(2),S("ngClass",Ne(2,tt,"methodfullcoverage"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"methodfullcoverage"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"methodfullcoverage"!==t.settings.sortBy)),f(),k(t.translations.percentage)}}function z3(e,n){if(1&e){const t=le();v(0,"th")(1,"a",2),j("click",function(i){const r=H(t).$implicit;return B(m(2).updateSorting(r.abbreviation,i))}),x(2,"i",24),D(3),_(),v(4,"a",41),x(5,"i",42),_()()}if(2&e){const t=n.$implicit,o=m(2);f(2),S("ngClass",Ne(4,tt,o.settings.sortBy===t.abbreviation&&"asc"===o.settings.sortOrder,o.settings.sortBy===t.abbreviation&&"desc"===o.settings.sortOrder,o.settings.sortBy!==t.abbreviation)),f(),k(t.name),f(),S("href",Mn(t.explanationUrl),jn)}}function G3(e,n){if(1&e&&x(0,"tr",43),2&e){const t=m().$implicit,o=m(2);S("element",t)("collapsed",t.collapsed)("lineCoverageAvailable",o.settings.showLineCoverage)("branchCoverageAvailable",o.branchCoverageAvailable&&o.settings.showBranchCoverage)("methodCoverageAvailable",o.methodCoverageAvailable&&o.settings.showMethodCoverage)("methodFullCoverageAvailable",o.methodCoverageAvailable&&o.settings.showFullMethodCoverage)("visibleMetrics",o.settings.visibleMetrics)}}function q3(e,n){if(1&e&&x(0,"tr",44),2&e){const t=m().$implicit,o=m(3);S("clazz",t)("translations",o.translations)("lineCoverageAvailable",o.settings.showLineCoverage)("branchCoverageAvailable",o.branchCoverageAvailable&&o.settings.showBranchCoverage)("methodCoverageAvailable",o.methodCoverageAvailable&&o.settings.showMethodCoverage)("methodFullCoverageAvailable",o.methodCoverageAvailable&&o.settings.showFullMethodCoverage)("visibleMetrics",o.settings.visibleMetrics)("historyComparisionDate",o.settings.historyComparisionDate)}}function W3(e,n){if(1&e&&y(0,q3,1,8,"tr",44),2&e){const t=n.$implicit,o=m().$implicit,i=m(2);C(!o.collapsed&&t.visible(i.settings)?0:-1)}}function Y3(e,n){if(1&e&&x(0,"tr",46),2&e){const t=m().$implicit,o=m(5);S("clazz",t)("translations",o.translations)("lineCoverageAvailable",o.settings.showLineCoverage)("branchCoverageAvailable",o.branchCoverageAvailable&&o.settings.showBranchCoverage)("methodCoverageAvailable",o.methodCoverageAvailable&&o.settings.showMethodCoverage)("methodFullCoverageAvailable",o.methodCoverageAvailable&&o.settings.showFullMethodCoverage)("visibleMetrics",o.settings.visibleMetrics)("historyComparisionDate",o.settings.historyComparisionDate)}}function J3(e,n){if(1&e&&y(0,Y3,1,8,"tr",46),2&e){const t=n.$implicit,o=m(2).$implicit,i=m(3);C(!o.collapsed&&t.visible(i.settings)?0:-1)}}function Z3(e,n){if(1&e&&(x(0,"tr",45),Ke(1,J3,1,1,null,null,Qe)),2&e){const t=m().$implicit,o=m(3);S("element",t)("collapsed",t.collapsed)("lineCoverageAvailable",o.settings.showLineCoverage)("branchCoverageAvailable",o.branchCoverageAvailable&&o.settings.showBranchCoverage)("methodCoverageAvailable",o.methodCoverageAvailable&&o.settings.showMethodCoverage)("methodFullCoverageAvailable",o.methodCoverageAvailable&&o.settings.showFullMethodCoverage)("visibleMetrics",o.settings.visibleMetrics),f(),Xe(t.classes)}}function Q3(e,n){if(1&e&&y(0,Z3,3,7),2&e){const t=n.$implicit,o=m().$implicit,i=m(2);C(!o.collapsed&&t.visible(i.settings)?0:-1)}}function K3(e,n){if(1&e&&(y(0,G3,1,7,"tr",43),Ke(1,W3,1,1,null,null,Qe),Ke(3,Q3,1,1,null,null,Qe)),2&e){const t=n.$implicit,o=m(2);C(t.visible(o.settings)?0:-1),f(),Xe(t.classes),f(2),Xe(t.subElements)}}function X3(e,n){if(1&e){const t=le();v(0,"div"),y(1,jj,1,10,"popup",0),v(2,"div",1)(3,"div")(4,"a",2),j("click",function(i){return H(t),B(m().collapseAll(i))}),D(5),_(),D(6," | "),v(7,"a",2),j("click",function(i){return H(t),B(m().expandAll(i))}),D(8),_()(),v(9,"div",3)(10,"span",4),y(11,Uj,1,1),y(12,$j,1,1),y(13,zj,1,1),_(),x(14,"br"),D(15),v(16,"input",5),ze("ngModelChange",function(i){H(t);const r=m();return _e(r.settings.grouping,i)||(r.settings.grouping=i),B(i)}),j("ngModelChange",function(){return H(t),B(m().updateCoverageInfo())}),_()(),v(17,"div",3),y(18,e3,9,5),_(),v(19,"div",6)(20,"button",7),j("click",function(){return H(t),B(m().popupVisible=!0)}),x(21,"i",8),D(22),_()()(),v(23,"div",9)(24,"table",10)(25,"colgroup"),x(26,"col",11),y(27,t3,1,0,"col",12),y(28,n3,1,0,"col",13),y(29,o3,1,0,"col",14),y(30,i3,1,0,"col",15),y(31,r3,1,0,"col",16),y(32,s3,1,0,"col",17),y(33,a3,1,0,"col",12),y(34,l3,1,0,"col",15),y(35,c3,1,0,"col",16),y(36,u3,1,0,"col",17),y(37,d3,1,0,"col",12),y(38,f3,1,0,"col",15),y(39,h3,1,0,"col",16),y(40,g3,1,0,"col",17),y(41,p3,1,0,"col",12),y(42,m3,1,0,"col",15),y(43,_3,1,0,"col",16),y(44,v3,1,0,"col",17),Ke(45,y3,1,0,"col",17,Qe),_(),v(47,"thead")(48,"tr",18),x(49,"th"),y(50,C3,2,1,"th",19),y(51,b3,2,1,"th",20),y(52,D3,2,1,"th",20),y(53,w3,2,1,"th",20),y(54,E3,2,2,"th",21),_(),v(55,"tr",22)(56,"td")(57,"input",23),ze("ngModelChange",function(i){H(t);const r=m();return _e(r.settings.filter,i)||(r.settings.filter=i),B(i)}),_()(),y(58,I3,2,3,"td",19),y(59,M3,2,3,"td",20),y(60,T3,2,3,"td",20),y(61,S3,2,3,"td",20),y(62,O3,1,1,"td",21),_(),v(63,"tr")(64,"th")(65,"a",2),j("click",function(i){return H(t),B(m().updateSorting("name",i))}),x(66,"i",24),D(67),_()(),y(68,N3,4,6,"th",25),y(69,A3,4,6,"th",25),y(70,x3,4,6,"th",25),y(71,k3,4,6,"th",25),y(72,R3,4,6,"th",26),y(73,F3,4,6,"th",25),y(74,L3,4,6,"th",25),y(75,P3,4,6,"th",26),y(76,V3,4,6,"th",25),y(77,H3,4,6,"th",25),y(78,B3,4,6,"th",26),y(79,j3,4,6,"th",25),y(80,U3,4,6,"th",25),y(81,$3,4,6,"th",26),Ke(82,z3,6,8,"th",null,Qe),_()(),v(84,"tbody"),Ke(85,K3,5,1,null,null,Qe),_()()()()}if(2&e){const t=m();f(),C(t.popupVisible?1:-1),f(4),k(t.translations.collapseAll),f(3),k(t.translations.expandAll),f(3),C(-1===t.settings.grouping?11:-1),f(),C(0===t.settings.grouping?12:-1),f(),C(t.settings.grouping>0?13:-1),f(2),L(" ",t.translations.grouping," "),f(),S("max",t.settings.groupingMaximum),He("ngModel",t.settings.grouping),f(2),C(t.historicCoverageExecutionTimes.length>0?18:-1),f(4),k(t.metrics.length>0?t.translations.selectCoverageTypesAndMetrics:t.translations.selectCoverageTypes),f(5),C(t.settings.showLineCoverage?27:-1),f(),C(t.settings.showLineCoverage?28:-1),f(),C(t.settings.showLineCoverage?29:-1),f(),C(t.settings.showLineCoverage?30:-1),f(),C(t.settings.showLineCoverage?31:-1),f(),C(t.settings.showLineCoverage?32:-1),f(),C(t.branchCoverageAvailable&&t.settings.showBranchCoverage?33:-1),f(),C(t.branchCoverageAvailable&&t.settings.showBranchCoverage?34:-1),f(),C(t.branchCoverageAvailable&&t.settings.showBranchCoverage?35:-1),f(),C(t.branchCoverageAvailable&&t.settings.showBranchCoverage?36:-1),f(),C(t.methodCoverageAvailable&&t.settings.showMethodCoverage?37:-1),f(),C(t.methodCoverageAvailable&&t.settings.showMethodCoverage?38:-1),f(),C(t.methodCoverageAvailable&&t.settings.showMethodCoverage?39:-1),f(),C(t.methodCoverageAvailable&&t.settings.showMethodCoverage?40:-1),f(),C(t.methodCoverageAvailable&&t.settings.showFullMethodCoverage?41:-1),f(),C(t.methodCoverageAvailable&&t.settings.showFullMethodCoverage?42:-1),f(),C(t.methodCoverageAvailable&&t.settings.showFullMethodCoverage?43:-1),f(),C(t.methodCoverageAvailable&&t.settings.showFullMethodCoverage?44:-1),f(),Xe(t.settings.visibleMetrics),f(5),C(t.settings.showLineCoverage?50:-1),f(),C(t.branchCoverageAvailable&&t.settings.showBranchCoverage?51:-1),f(),C(t.methodCoverageAvailable&&t.settings.showMethodCoverage?52:-1),f(),C(t.methodCoverageAvailable&&t.settings.showFullMethodCoverage?53:-1),f(),C(t.settings.visibleMetrics.length>0?54:-1),f(3),S("placeholder",Mn(t.translations.filter)),He("ngModel",t.settings.filter),f(),C(t.settings.showLineCoverage?58:-1),f(),C(t.branchCoverageAvailable&&t.settings.showBranchCoverage?59:-1),f(),C(t.methodCoverageAvailable&&t.settings.showMethodCoverage?60:-1),f(),C(t.methodCoverageAvailable&&t.settings.showFullMethodCoverage?61:-1),f(),C(t.settings.visibleMetrics.length>0?62:-1),f(4),S("ngClass",Ne(58,tt,"name"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"name"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"name"!==t.settings.sortBy)),f(),k(t.translations.name),f(),C(t.settings.showLineCoverage?68:-1),f(),C(t.settings.showLineCoverage?69:-1),f(),C(t.settings.showLineCoverage?70:-1),f(),C(t.settings.showLineCoverage?71:-1),f(),C(t.settings.showLineCoverage?72:-1),f(),C(t.branchCoverageAvailable&&t.settings.showBranchCoverage?73:-1),f(),C(t.branchCoverageAvailable&&t.settings.showBranchCoverage?74:-1),f(),C(t.branchCoverageAvailable&&t.settings.showBranchCoverage?75:-1),f(),C(t.methodCoverageAvailable&&t.settings.showMethodCoverage?76:-1),f(),C(t.methodCoverageAvailable&&t.settings.showMethodCoverage?77:-1),f(),C(t.methodCoverageAvailable&&t.settings.showMethodCoverage?78:-1),f(),C(t.methodCoverageAvailable&&t.settings.showFullMethodCoverage?79:-1),f(),C(t.methodCoverageAvailable&&t.settings.showFullMethodCoverage?80:-1),f(),C(t.methodCoverageAvailable&&t.settings.showFullMethodCoverage?81:-1),f(),Xe(t.settings.visibleMetrics),f(3),Xe(t.codeElements)}}let eU=(()=>{class e{constructor(t){this.queryString="",this.historicCoverageExecutionTimes=[],this.branchCoverageAvailable=!1,this.methodCoverageAvailable=!1,this.metrics=[],this.codeElements=[],this.translations={},this.popupVisible=!1,this.settings=new t8,this.sliderOptions={floor:0,ceil:100,step:1,ticksArray:[0,10,20,30,40,50,60,70,80,90,100],showTicks:!0},this.window=t.nativeWindow}ngOnInit(){this.historicCoverageExecutionTimes=this.window.historicCoverageExecutionTimes,this.branchCoverageAvailable=this.window.branchCoverageAvailable,this.methodCoverageAvailable=this.window.methodCoverageAvailable,this.metrics=this.window.metrics,this.translations=this.window.translations,xt.maximumDecimalPlacesForCoverageQuotas=this.window.maximumDecimalPlacesForCoverageQuotas;let t=!1;if(void 0!==this.window.history&&void 0!==this.window.history.replaceState&&null!==this.window.history.state&&null!=this.window.history.state.coverageInfoSettings)console.log("Coverage info: Restoring from history",this.window.history.state.coverageInfoSettings),t=!0,this.settings=JSON.parse(JSON.stringify(this.window.history.state.coverageInfoSettings));else{let i=0,r=this.window.assemblies;for(let s=0;s<r.length;s++)for(let a=0;a<r[s].classes.length;a++)i=Math.max(i,(r[s].classes[a].name.match(/\.|\\/g)||[]).length);this.settings.groupingMaximum=i,console.log("Grouping maximum: "+i),this.window.applyMaximumGroupingLevel&&(this.settings.grouping=i),this.settings.showBranchCoverage=this.branchCoverageAvailable,this.settings.showMethodCoverage=this.methodCoverageAvailable,this.settings.showFullMethodCoverage=this.methodCoverageAvailable}const o=window.location.href.indexOf("?");o>-1&&(this.queryString=window.location.href.substring(o)),this.updateCoverageInfo(),t&&this.restoreCollapseState()}onBeforeUnload(){if(this.saveCollapseState(),void 0!==this.window.history&&void 0!==this.window.history.replaceState){console.log("Coverage info: Updating history",this.settings);let t=new i1;null!==window.history.state&&(t=JSON.parse(JSON.stringify(this.window.history.state))),t.coverageInfoSettings=JSON.parse(JSON.stringify(this.settings)),window.history.replaceState(t,"")}}updateCoverageInfo(){let t=(new Date).getTime(),o=this.window.assemblies,i=[],r=0;if(0===this.settings.grouping)for(let l=0;l<o.length;l++){let c=new no(o[l].name,null);i.push(c);for(let u=0;u<o[l].classes.length;u++)c.insertClass(new sg(o[l].classes[u],this.queryString),null),r++}else if(-1===this.settings.grouping){let l=new no(this.translations.all,null);i.push(l);for(let c=0;c<o.length;c++)for(let u=0;u<o[c].classes.length;u++)l.insertClass(new sg(o[c].classes[u],this.queryString),null),r++}else for(let l=0;l<o.length;l++){let c=new no(o[l].name,null);i.push(c);for(let u=0;u<o[l].classes.length;u++)c.insertClass(new sg(o[l].classes[u],this.queryString),this.settings.grouping),r++}let s=-1,a=1;"name"===this.settings.sortBy&&(s="asc"===this.settings.sortOrder?-1:1,a="asc"===this.settings.sortOrder?1:-1),i.sort(function(l,c){return l.name===c.name?0:l.name<c.name?s:a}),no.sortCodeElementViewModels(i,this.settings.sortBy,"asc"===this.settings.sortOrder);for(let l=0;l<i.length;l++)i[l].changeSorting(this.settings.sortBy,"asc"===this.settings.sortOrder);this.codeElements=i,console.log(`Processing assemblies finished (Duration: ${(new Date).getTime()-t}ms, Assemblies: ${i.length}, Classes: ${r})`),""!==this.settings.historyComparisionDate&&this.updateCurrentHistoricCoverage()}updateCurrentHistoricCoverage(){let t=(new Date).getTime();for(let o=0;o<this.codeElements.length;o++)this.codeElements[o].updateCurrentHistoricCoverage(this.settings.historyComparisionDate);console.log(`Updating current historic coverage finished (Duration: ${(new Date).getTime()-t}ms)`)}collapseAll(t){t.preventDefault();for(let o=0;o<this.codeElements.length;o++)this.codeElements[o].collapse()}expandAll(t){t.preventDefault();for(let o=0;o<this.codeElements.length;o++)this.codeElements[o].expand()}updateSorting(t,o){o.preventDefault(),this.settings.sortOrder=t===this.settings.sortBy&&"asc"===this.settings.sortOrder?"desc":"asc",this.settings.sortBy=t,console.log(`Updating sort column: '${this.settings.sortBy}' (${this.settings.sortOrder})`),no.sortCodeElementViewModels(this.codeElements,this.settings.sortBy,"asc"===this.settings.sortOrder);for(let i=0;i<this.codeElements.length;i++)this.codeElements[i].changeSorting(this.settings.sortBy,"asc"===this.settings.sortOrder)}saveCollapseState(){this.settings.collapseStates=[];let t=o=>{for(let i=0;i<o.length;i++)this.settings.collapseStates.push(o[i].collapsed),t(o[i].subElements)};t(this.codeElements)}restoreCollapseState(){let t=0,o=i=>{for(let r=0;r<i.length;r++)this.settings.collapseStates.length>t&&(i[r].collapsed=this.settings.collapseStates[t]),t++,o(i[r].subElements)};o(this.codeElements)}static#e=this.\u0275fac=function(o){return new(o||e)(T(ag))};static#t=this.\u0275cmp=Wt({type:e,selectors:[["coverage-info"]],hostBindings:function(o,i){1&o&&j("beforeunload",function(){return i.onBeforeUnload()},Oa)},standalone:!1,decls:1,vars:1,consts:[[3,"visible","translations","branchCoverageAvailable","methodCoverageAvailable","metrics","showLineCoverage","showBranchCoverage","showMethodCoverage","showMethodFullCoverage","visibleMetrics"],[1,"customizebox"],["href","#",3,"click"],[1,"col-center"],[1,"slider-label"],["type","range","step","1","min","-1",3,"ngModelChange","max","ngModel"],[1,"col-right","right"],["type","button",3,"click"],[1,"icon-cog"],[1,"table-responsive"],[1,"overview","table-fixed","stripped"],[1,"column-min-200"],[1,"column90"],[1,"column105"],[1,"column100"],[1,"column70"],[1,"column98"],[1,"column112"],[1,"header"],["colspan","6",1,"center"],["colspan","4",1,"center"],[1,"center"],[1,"filterbar"],["type","search",3,"ngModelChange","ngModel","placeholder"],[3,"ngClass"],[1,"right"],["colspan","2",1,"center"],[3,"visibleChange","showLineCoverageChange","showBranchCoverageChange","showMethodCoverageChange","showMethodFullCoverageChange","visibleMetricsChange","visible","translations","branchCoverageAvailable","methodCoverageAvailable","metrics","showLineCoverage","showBranchCoverage","showMethodCoverage","showMethodFullCoverage","visibleMetrics"],[3,"ngModelChange","ngModel"],["value",""],[3,"value"],["value","allChanges"],["value","lineCoverageIncreaseOnly"],["value","lineCoverageDecreaseOnly"],["value","branchCoverageIncreaseOnly"],["value","branchCoverageDecreaseOnly"],["value","methodCoverageIncreaseOnly"],["value","methodCoverageDecreaseOnly"],["value","fullMethodCoverageIncreaseOnly"],["value","fullMethodCoverageDecreaseOnly"],[3,"valueChange","highValueChange","value","highValue","options"],["target","_blank",3,"href"],[1,"icon-info-circled"],["codeelement-row","",3,"element","collapsed","lineCoverageAvailable","branchCoverageAvailable","methodCoverageAvailable","methodFullCoverageAvailable","visibleMetrics"],["class-row","",3,"clazz","translations","lineCoverageAvailable","branchCoverageAvailable","methodCoverageAvailable","methodFullCoverageAvailable","visibleMetrics","historyComparisionDate"],["codeelement-row","",1,"namespace",3,"element","collapsed","lineCoverageAvailable","branchCoverageAvailable","methodCoverageAvailable","methodFullCoverageAvailable","visibleMetrics"],["class-row","",1,"namespace",3,"clazz","translations","lineCoverageAvailable","branchCoverageAvailable","methodCoverageAvailable","methodFullCoverageAvailable","visibleMetrics","historyComparisionDate"]],template:function(o,i){1&o&&y(0,X3,87,62,"div"),2&o&&C(i.codeElements.length>0?0:-1)},dependencies:[qi,eg,ng,Cs,Xh,Ts,ql,Is,o1,f8,P8,Bj],encapsulation:2})}return e})();class tU{constructor(){this.assembly="",this.numberOfRiskHotspots=10,this.filter="",this.sortBy="",this.sortOrder="asc"}}const dc=(e,n,t)=>({"icon-up-dir_active":e,"icon-down-dir_active":n,"icon-up-down-dir":t}),nU=(e,n)=>({lightred:e,lightgreen:n});function oU(e,n){if(1&e&&(v(0,"option",3),D(1),_()),2&e){const t=n.$implicit;S("value",t),f(),k(t)}}function iU(e,n){if(1&e&&(v(0,"span"),D(1),_()),2&e){const t=m(2);f(),k(t.translations.top)}}function rU(e,n){1&e&&(v(0,"option",16),D(1,"20"),_())}function sU(e,n){1&e&&(v(0,"option",17),D(1,"50"),_())}function aU(e,n){1&e&&(v(0,"option",18),D(1,"100"),_())}function lU(e,n){if(1&e&&(v(0,"option",3),D(1),_()),2&e){const t=m(3);S("value",t.totalNumberOfRiskHotspots),f(),k(t.translations.all)}}function cU(e,n){if(1&e){const t=le();v(0,"select",14),ze("ngModelChange",function(i){H(t);const r=m(2);return _e(r.settings.numberOfRiskHotspots,i)||(r.settings.numberOfRiskHotspots=i),B(i)}),v(1,"option",15),D(2,"10"),_(),y(3,rU,2,0,"option",16),y(4,sU,2,0,"option",17),y(5,aU,2,0,"option",18),y(6,lU,2,2,"option",3),_()}if(2&e){const t=m(2);He("ngModel",t.settings.numberOfRiskHotspots),f(3),C(t.totalNumberOfRiskHotspots>10?3:-1),f(),C(t.totalNumberOfRiskHotspots>20?4:-1),f(),C(t.totalNumberOfRiskHotspots>50?5:-1),f(),C(t.totalNumberOfRiskHotspots>100?6:-1)}}function uU(e,n){1&e&&x(0,"col",11)}function dU(e,n){if(1&e){const t=le();v(0,"th")(1,"a",12),j("click",function(i){const r=H(t).$index;return B(m(2).updateSorting(""+r,i))}),x(2,"i",13),D(3),_(),v(4,"a",19),x(5,"i",20),_()()}if(2&e){const t=n.$implicit,o=n.$index,i=m(2);f(2),S("ngClass",Ne(4,dc,i.settings.sortBy===""+o&&"asc"===i.settings.sortOrder,i.settings.sortBy===""+o&&"desc"===i.settings.sortOrder,i.settings.sortBy!==""+o)),f(),k(t.name),f(),S("href",Mn(t.explanationUrl),jn)}}function fU(e,n){if(1&e&&(v(0,"td",23),D(1),_()),2&e){const t=n.$implicit;S("ngClass",jf(2,nU,t.exceeded,!t.exceeded)),f(),k(t.value)}}function hU(e,n){if(1&e&&(v(0,"tr")(1,"td"),D(2),_(),v(3,"td")(4,"a",21),D(5),_()(),v(6,"td",22)(7,"a",21),D(8),_()(),Ke(9,fU,2,5,"td",23,Qe),_()),2&e){const t=n.$implicit,o=m(2);f(2),k(t.assembly),f(2),S("href",t.reportPath+o.queryString,jn),f(),k(t.class),f(),S("title",t.methodName),f(),S("href",t.reportPath+o.queryString+"#file"+t.fileIndex+"_line"+t.line,jn),f(),L(" ",t.methodShortName," "),f(),Xe(t.metrics)}}function gU(e,n){if(1&e){const t=le();v(0,"div")(1,"div",0)(2,"div")(3,"select",1),ze("ngModelChange",function(i){H(t);const r=m();return _e(r.settings.assembly,i)||(r.settings.assembly=i),B(i)}),j("ngModelChange",function(){return H(t),B(m().updateRiskHotpots())}),v(4,"option",2),D(5),_(),Ke(6,oU,2,2,"option",3,Qe),_()(),v(8,"div",4),y(9,iU,2,1,"span"),y(10,cU,7,5,"select",5),_(),x(11,"div",4),v(12,"div",6)(13,"span"),D(14),_(),v(15,"input",7),ze("ngModelChange",function(i){H(t);const r=m();return _e(r.settings.filter,i)||(r.settings.filter=i),B(i)}),j("ngModelChange",function(){return H(t),B(m().updateRiskHotpots())}),_()()(),v(16,"div",8)(17,"table",9)(18,"colgroup"),x(19,"col",10)(20,"col",10)(21,"col",10),Ke(22,uU,1,0,"col",11,Qe),_(),v(24,"thead")(25,"tr")(26,"th")(27,"a",12),j("click",function(i){return H(t),B(m().updateSorting("assembly",i))}),x(28,"i",13),D(29),_()(),v(30,"th")(31,"a",12),j("click",function(i){return H(t),B(m().updateSorting("class",i))}),x(32,"i",13),D(33),_()(),v(34,"th")(35,"a",12),j("click",function(i){return H(t),B(m().updateSorting("method",i))}),x(36,"i",13),D(37),_()(),Ke(38,dU,6,8,"th",null,Qe),_()(),v(40,"tbody"),Ke(41,hU,11,6,"tr",null,Qe),function yb(e,n){const t=J();let o;const i=e+q;t.firstCreatePass?(o=function KR(e,n){if(n)for(let t=n.length-1;t>=0;t--){const o=n[t];if(e===o.name)return o}}(n,t.pipeRegistry),t.data[i]=o,o.onDestroy&&(t.destroyHooks??=[]).push(i,o.onDestroy)):o=t.data[i];const r=o.factory||(o.factory=co(o.type)),a=ht(T);try{const l=ua(!1),c=r();return ua(l),function Qc(e,n,t,o){t>=e.data.length&&(e.data[t]=null,e.blueprint[t]=null),n[t]=o}(t,w(),i,c),c}finally{ht(a)}}(43,"slice"),_()()()()}if(2&e){const t=m();f(3),He("ngModel",t.settings.assembly),f(2),k(t.translations.assembly),f(),Xe(t.assemblies),f(3),C(t.totalNumberOfRiskHotspots>10?9:-1),f(),C(t.totalNumberOfRiskHotspots>10?10:-1),f(4),L("",t.translations.filter," "),f(),He("ngModel",t.settings.filter),f(7),Xe(t.riskHotspotMetrics),f(6),S("ngClass",Ne(16,dc,"assembly"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"assembly"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"assembly"!==t.settings.sortBy)),f(),k(t.translations.assembly),f(3),S("ngClass",Ne(20,dc,"class"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"class"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"class"!==t.settings.sortBy)),f(),k(t.translations.class),f(3),S("ngClass",Ne(24,dc,"method"===t.settings.sortBy&&"asc"===t.settings.sortOrder,"method"===t.settings.sortBy&&"desc"===t.settings.sortOrder,"method"!==t.settings.sortBy)),f(),k(t.translations.method),f(),Xe(t.riskHotspotMetrics),f(3),Xe(function Cb(e,n,t,o,i){const r=e+q,s=w(),a=function _o(e,n){return e[n]}(s,r);return function us(e,n){return e[1].data[n].pure}(s,r)?mb(s,st(),n,a.transform,t,o,i,a):a.transform(t,o,i)}(43,12,t.riskHotspots,0,t.settings.numberOfRiskHotspots))}}let pU=(()=>{class e{constructor(t){this.queryString="",this.riskHotspotMetrics=[],this.riskHotspots=[],this.totalNumberOfRiskHotspots=0,this.assemblies=[],this.translations={},this.settings=new tU,this.window=t.nativeWindow}ngOnInit(){this.riskHotspotMetrics=this.window.riskHotspotMetrics,this.translations=this.window.translations,void 0!==this.window.history&&void 0!==this.window.history.replaceState&&null!==this.window.history.state&&null!=this.window.history.state.riskHotspotsSettings&&(console.log("Risk hotspots: Restoring from history",this.window.history.state.riskHotspotsSettings),this.settings=JSON.parse(JSON.stringify(this.window.history.state.riskHotspotsSettings)));const t=window.location.href.indexOf("?");t>-1&&(this.queryString=window.location.href.substring(t)),this.updateRiskHotpots()}onDonBeforeUnlodad(){if(void 0!==this.window.history&&void 0!==this.window.history.replaceState){console.log("Risk hotspots: Updating history",this.settings);let t=new i1;null!==window.history.state&&(t=JSON.parse(JSON.stringify(this.window.history.state))),t.riskHotspotsSettings=JSON.parse(JSON.stringify(this.settings)),window.history.replaceState(t,"")}}updateRiskHotpots(){const t=this.window.riskHotspots;if(this.totalNumberOfRiskHotspots=t.length,0===this.assemblies.length){let s=[];for(let a=0;a<t.length;a++)-1===s.indexOf(t[a].assembly)&&s.push(t[a].assembly);this.assemblies=s.sort()}let o=[];for(let s=0;s<t.length;s++)""!==this.settings.filter&&-1===t[s].class.toLowerCase().indexOf(this.settings.filter.toLowerCase())||""!==this.settings.assembly&&t[s].assembly!==this.settings.assembly||o.push(t[s]);let i="asc"===this.settings.sortOrder?-1:1,r="asc"===this.settings.sortOrder?1:-1;if("assembly"===this.settings.sortBy)o.sort(function(s,a){return s.assembly===a.assembly?0:s.assembly<a.assembly?i:r});else if("class"===this.settings.sortBy)o.sort(function(s,a){return s.class===a.class?0:s.class<a.class?i:r});else if("method"===this.settings.sortBy)o.sort(function(s,a){return s.methodShortName===a.methodShortName?0:s.methodShortName<a.methodShortName?i:r});else if(""!==this.settings.sortBy){let s=parseInt(this.settings.sortBy,10);o.sort(function(a,l){return a.metrics[s].value===l.metrics[s].value?0:a.metrics[s].value<l.metrics[s].value?i:r})}this.riskHotspots=o}updateSorting(t,o){o.preventDefault(),this.settings.sortOrder=t===this.settings.sortBy&&"asc"===this.settings.sortOrder?"desc":"asc",this.settings.sortBy=t,console.log(`Updating sort column: '${this.settings.sortBy}' (${this.settings.sortOrder})`),this.updateRiskHotpots()}static#e=this.\u0275fac=function(o){return new(o||e)(T(ag))};static#t=this.\u0275cmp=Wt({type:e,selectors:[["risk-hotspots"]],hostBindings:function(o,i){1&o&&j("beforeunload",function(){return i.onDonBeforeUnlodad()},Oa)},standalone:!1,decls:1,vars:1,consts:[[1,"customizebox"],["name","assembly",3,"ngModelChange","ngModel"],["value",""],[3,"value"],[1,"col-center"],[3,"ngModel"],[1,"col-right"],["type","search",3,"ngModelChange","ngModel"],[1,"table-responsive"],[1,"overview","table-fixed","stripped"],[1,"column-min-200"],[1,"column105"],["href","#",3,"click"],[3,"ngClass"],[3,"ngModelChange","ngModel"],["value","10"],["value","20"],["value","50"],["value","100"],["target","_blank",3,"href"],[1,"icon-info-circled"],[3,"href"],[3,"title"],[1,"right",3,"ngClass"]],template:function(o,i){1&o&&y(0,gU,44,28,"div"),2&o&&C(i.totalNumberOfRiskHotspots>0?0:-1)},dependencies:[qi,eg,ng,Cs,Ts,ql,Is,aw],encapsulation:2})}return e})(),mU=(()=>{class e{static#e=this.\u0275fac=function(o){return new(o||e)};static#t=this.\u0275mod=Gn({type:e,bootstrap:[pU,eU]});static#n=this.\u0275inj=dn({providers:[ag],imports:[fV,dB,e8]})}return e})();dV().bootstrapModule(mU).catch(e=>console.error(e))}},Wo=>{Wo(Wo.s=551)}]);