/**
 * Identity Particles — Canvas-based floating particle animation with connection lines.
 * Used on the identity pages (Register, Confirm, Forgot Password, Reset Password).
 *
 * Features:
 *   - Floating particles (small circles, white/light blue, low opacity)
 *   - Connection lines between nearby particles
 *   - Mouse interaction (particles gently pushed away by cursor proximity)
 *   - Respects prefers-reduced-motion: reduce (no animation when enabled)
 *   - Handles window resize to keep canvas full-screen
 *   - Uses requestAnimationFrame for smooth animation
 *
 * Canvas element expected: <canvas id="particle-canvas"></canvas>
 */
(function () {
    'use strict';

    // =========================================================================
    // Configuration
    // =========================================================================
    var PARTICLE_COUNT = 45;
    var PARTICLE_MIN_RADIUS = 2;
    var PARTICLE_MAX_RADIUS = 4;
    var CONNECTION_DISTANCE = 150;
    var MOUSE_RADIUS = 120;
    var MOUSE_PUSH_FORCE = 2;
    var BASE_SPEED = 0.4;

    // Particle colours (white and light blue with low opacity)
    var PARTICLE_COLOURS = [
        'rgba(255, 255, 255, 0.6)',
        'rgba(255, 255, 255, 0.4)',
        'rgba(87, 184, 232, 0.5)',
        'rgba(87, 184, 232, 0.35)',
        'rgba(200, 220, 255, 0.45)'
    ];

    var LINE_COLOUR_BASE = 'rgba(255, 255, 255, ';

    // =========================================================================
    // State
    // =========================================================================
    var canvas = null;
    var ctx = null;
    var particles = [];
    var animationId = null;
    var mouse = { x: -9999, y: -9999 };

    // =========================================================================
    // Initialization
    // =========================================================================
    function init() {
        // Respect prefers-reduced-motion
        var motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
        if (motionQuery.matches) {
            return; // Do not start animation
        }

        canvas = document.getElementById('particle-canvas');
        if (!canvas) return;

        ctx = canvas.getContext('2d');
        if (!ctx) return;

        resizeCanvas();
        createParticles();
        bindEvents(motionQuery);
        animate();
    }

    // =========================================================================
    // Canvas Setup
    // =========================================================================
    function resizeCanvas() {
        if (!canvas) return;
        canvas.width = window.innerWidth;
        canvas.height = window.innerHeight;
    }

    // =========================================================================
    // Particle Creation
    // =========================================================================
    function createParticles() {
        particles = [];
        for (var i = 0; i < PARTICLE_COUNT; i++) {
            particles.push(createParticle());
        }
    }

    function createParticle() {
        var radius = PARTICLE_MIN_RADIUS + Math.random() * (PARTICLE_MAX_RADIUS - PARTICLE_MIN_RADIUS);
        return {
            x: Math.random() * canvas.width,
            y: Math.random() * canvas.height,
            vx: (Math.random() - 0.5) * BASE_SPEED * 2,
            vy: (Math.random() - 0.5) * BASE_SPEED * 2,
            radius: radius,
            colour: PARTICLE_COLOURS[Math.floor(Math.random() * PARTICLE_COLOURS.length)]
        };
    }

    // =========================================================================
    // Event Binding
    // =========================================================================
    function bindEvents(motionQuery) {
        window.addEventListener('resize', handleResize);
        window.addEventListener('mousemove', handleMouseMove);
        window.addEventListener('mouseleave', handleMouseLeave);

        // Listen for motion preference changes (user toggles reduced motion)
        motionQuery.addEventListener('change', function (e) {
            if (e.matches) {
                stopAnimation();
            } else {
                if (!animationId) {
                    animate();
                }
            }
        });
    }

    function handleResize() {
        resizeCanvas();
        // Reposition particles that are now outside bounds
        for (var i = 0; i < particles.length; i++) {
            var p = particles[i];
            if (p.x > canvas.width) p.x = canvas.width * Math.random();
            if (p.y > canvas.height) p.y = canvas.height * Math.random();
        }
    }

    function handleMouseMove(e) {
        mouse.x = e.clientX;
        mouse.y = e.clientY;
    }

    function handleMouseLeave() {
        mouse.x = -9999;
        mouse.y = -9999;
    }

    // =========================================================================
    // Animation Loop
    // =========================================================================
    function animate() {
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        updateParticles();
        drawConnections();
        drawParticles();
        animationId = requestAnimationFrame(animate);
    }

    function stopAnimation() {
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
    }

    // =========================================================================
    // Particle Update (Movement + Mouse Interaction)
    // =========================================================================
    function updateParticles() {
        for (var i = 0; i < particles.length; i++) {
            var p = particles[i];

            // Mouse interaction — push particles away from cursor
            var dx = p.x - mouse.x;
            var dy = p.y - mouse.y;
            var dist = Math.sqrt(dx * dx + dy * dy);

            if (dist < MOUSE_RADIUS && dist > 0) {
                var force = (MOUSE_RADIUS - dist) / MOUSE_RADIUS;
                var angle = Math.atan2(dy, dx);
                p.vx += Math.cos(angle) * force * MOUSE_PUSH_FORCE * 0.05;
                p.vy += Math.sin(angle) * force * MOUSE_PUSH_FORCE * 0.05;
            }

            // Apply velocity with gentle damping
            p.x += p.vx;
            p.y += p.vy;
            p.vx *= 0.99;
            p.vy *= 0.99;

            // Ensure minimum drift speed so particles keep floating
            var speed = Math.sqrt(p.vx * p.vx + p.vy * p.vy);
            if (speed < BASE_SPEED * 0.3) {
                p.vx += (Math.random() - 0.5) * 0.1;
                p.vy += (Math.random() - 0.5) * 0.1;
            }

            // Wrap around edges
            if (p.x < -p.radius) p.x = canvas.width + p.radius;
            if (p.x > canvas.width + p.radius) p.x = -p.radius;
            if (p.y < -p.radius) p.y = canvas.height + p.radius;
            if (p.y > canvas.height + p.radius) p.y = -p.radius;
        }
    }

    // =========================================================================
    // Drawing — Connections
    // =========================================================================
    function drawConnections() {
        for (var i = 0; i < particles.length; i++) {
            for (var j = i + 1; j < particles.length; j++) {
                var dx = particles[i].x - particles[j].x;
                var dy = particles[i].y - particles[j].y;
                var dist = Math.sqrt(dx * dx + dy * dy);

                if (dist < CONNECTION_DISTANCE) {
                    var opacity = (1 - dist / CONNECTION_DISTANCE) * 0.3;
                    ctx.beginPath();
                    ctx.strokeStyle = LINE_COLOUR_BASE + opacity + ')';
                    ctx.lineWidth = 0.8;
                    ctx.moveTo(particles[i].x, particles[i].y);
                    ctx.lineTo(particles[j].x, particles[j].y);
                    ctx.stroke();
                }
            }
        }
    }

    // =========================================================================
    // Drawing — Particles
    // =========================================================================
    function drawParticles() {
        for (var i = 0; i < particles.length; i++) {
            var p = particles[i];
            ctx.beginPath();
            ctx.arc(p.x, p.y, p.radius, 0, Math.PI * 2);
            ctx.fillStyle = p.colour;
            ctx.fill();
        }
    }

    // =========================================================================
    // Self-Initialization
    // =========================================================================
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
