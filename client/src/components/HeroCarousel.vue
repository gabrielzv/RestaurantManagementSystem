<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";

/* Carousel slides */
type Slide = {
  src: string;
  title?: string;
  lead?: string;
  alt?: string;
};

const slides: Slide[] = [
  {
    src: "/images/hero/hero-carousel-1.jpg",
    title: "Sabores que inspiran",
    lead: "Descubre nuestro menú y vive la experiencia de la buena cocina.",
    alt: "Cliente disfrutando un platillo",
  },
  {
    src: "/images/hero/hero-carousel-2.jpg",
    title: "Ambiente acogedor",
    lead: "Ven con familia y amigos para una experiencia inolvidable.",
    alt: "Grupo de amigos compartiendo una comida",
  },
  {
    src: "/images/hero/hero-carousel-3.jpg",
    title: "Personal calificado",
    lead: "Cocineros expertos que garantizan la calidad en nuestros platillos.",
    alt: "Chef preparando un platillo",
  },
];

const current = ref(0);
const intervalMs = 5000;
let timer: number | undefined;

const next = () => {
  current.value = (current.value + 1) % slides.length;
};
const prev = () => {
  current.value = (current.value - 1 + slides.length) % slides.length;
};

const start = () => {
  stop();
  timer = window.setInterval(() => next(), intervalMs);
};
const stop = () => {
  if (timer !== undefined) window.clearInterval(timer);
  timer = undefined;
};

const goTo = (i: number) => {
  current.value = i % slides.length;
};

const scrollToMenu = () => {
  const el = document.getElementById("menu");
  if (el) el.scrollIntoView({ behavior: "smooth", block: "start" });
};

let touchStartX = 0;
let touchEndX = 0;
const onTouchStart = (e: TouchEvent) => {
  stop();
  const t = e.changedTouches && e.changedTouches[0];
  if (!t) return;
  touchStartX = t.clientX;
};

const onTouchEnd = (e: TouchEvent) => {
  const t = e.changedTouches && e.changedTouches[0];
  if (!t) {
    start();
    return;
  }
  touchEndX = t.clientX;
  const diff = touchStartX - touchEndX;
  if (Math.abs(diff) > 40) {
    if (diff > 0) next();
    else prev();
  }
  start();
};

onMounted(() => start());
onUnmounted(() => stop());
</script>

<template>
  <section class="hero-carousel" @mouseenter="stop" @mouseleave="start">
    <div class="slides" @touchstart.passive="onTouchStart" @touchend.passive="onTouchEnd">
      <div
        v-for="(slide, i) in slides"
        :key="slide.src"
        class="slide"
        :aria-hidden="current !== i"
        :class="{ active: current === i }"
        :style="{ backgroundImage: `url(${slide.src})` }"
      >
        <div class="overlay">
          <div class="inner">
            <h1>{{ slide.title }}</h1>
            <p class="lead">{{ slide.lead }}</p>
            <div class="actions">
              <button class="btn primary" @click="scrollToMenu" type="button">Ver menú</button>
              <button class="btn ghost" type="button" aria-disabled="true">Iniciar sesión</button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="controls">
      <button class="ctrl prev" @click="prev" aria-label="Anterior">‹</button>
      <button class="ctrl next" @click="next" aria-label="Siguiente">›</button>
    </div>

    <div class="dots">
      <button
        v-for="(slide, i) in slides"
        :key="slide.src"
        :class="['dot', { active: current === i }]"
        @click="goTo(i)"
        :aria-label="`Ir a slide ${i + 1}`"
      ></button>
    </div>
  </section>
</template>

<style scoped>
.hero-carousel {
  width: 100vw;
  margin-left: calc(50% - 50vw);
  position: relative;
  overflow: hidden;
  border-radius: 0;
  margin-bottom: 0;
  margin-top: 0;
  padding-top: 0;
}
.slides {
  position: relative;
  width: 100%;
  height: clamp(420px, 41vw, 960px);
  min-height: 420px;
  max-height: 960px;
}
.slide {
  position: absolute;
  inset: 0 0 0 0;
  width: 100%;
  background-size: cover;
  background-position: center center;
  transform: translateX(100%);
  transition: transform 480ms cubic-bezier(0.22, 0.9, 0.2, 1), opacity 480ms ease;
  opacity: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1;
}
.slide.active {
  transform: translateX(0);
  opacity: 1;
  z-index: 2;
}
.overlay {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(
    180deg,
    rgba(0, 0, 0, 0.35) 0%,
    rgba(0, 0, 0, 0.45) 40%,
    rgba(0, 0, 0, 0.55) 100%
  );
}
.inner {
  max-width: 980px;
  padding: 2rem;
  color: white;
  text-align: left;
}
.inner h1 {
  font-size: 2.25rem;
  margin: 0 0 0.5rem;
}
.lead {
  font-size: 1.05rem;
  opacity: 0.95;
  margin-bottom: 1rem;
}
.actions {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}
.btn {
  border: none;
  padding: 0.6rem 1rem;
  font-weight: 700;
  border-radius: 8px;
  cursor: pointer;
  border-radius: 999px;
}
.btn.primary {
  background: var(--color-accent);
  color: white;
}
.btn.ghost {
  background: transparent;
  color: white;
  border: 1px solid var(--color-accent);
}
.controls {
  position: absolute;
  top: 50%;
  left: 0;
  right: 0;
  display: flex;
  justify-content: space-between;
  transform: translateY(-50%);
  pointer-events: none;
  z-index: 6;
}
.ctrl {
  pointer-events: auto;
  background: rgba(0, 0, 0, 0.35);
  color: white;
  border: none;
  width: 44px;
  height: 44px;
  border-radius: 999px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin: 0 0.5rem;
  cursor: pointer;
  background-color: #361c0a;
  opacity: 0.5;
}
.dots {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  bottom: 12px;
  display: flex;
  gap: 8px;
  z-index: 6;
}
.dot {
  width: 10px;
  height: 10px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.42);
  border: none;
  cursor: pointer;
}
.dot.active {
  background: var(--color-accent);
}

@media (max-width: 720px) {
  .slides {
    height: 38vh;
    min-height: 220px;
  }
  .inner h1 {
    font-size: 1.4rem;
  }
}

@media (max-width: 420px) {
  .inner {
    padding: 1rem;
  }
  .actions {
    gap: 0.5rem;
  }
  .btn {
    padding: 0.5rem 0.8rem;
  }
}
</style>
